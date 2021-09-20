using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Olfactory.Comm;
using Olfactory.Utils;

namespace Olfactory.Tests.ThresholdTest
{
    public class ProcedurePens : ITestEmulator
    {
        /// <summary>
        /// Expected answer type
        /// </summary>
        public enum AnswerType
        {
            /// <summary>
            /// One button per pen: user presses it if think the order was on thin pen
            /// </summary>
            HasOdor,
            /// <summary>
            /// Two buttons per pen, "has-order" and "no-order"
            /// </summary>
            YesNo
        }
        
        public class PenActivationArgs : EventArgs
        {
            public int ID { get; private set; }
            public Settings.FlowStartTrigger FlowStart { get; private set; }
            public PenActivationArgs(int id, Settings.FlowStartTrigger flowStart)
            {
                ID = id;
                FlowStart = flowStart;
            }
        }

        /// <summary>
        /// Fires on progressing the odor preparation
        /// </summary>
        public event EventHandler<double> OdorPreparation = delegate { };
        /// <summary>
        /// Fires when some pen is activated, passes its ID
        /// </summary>
        public event EventHandler<PenActivationArgs> PenActivated = delegate { };
        /// <summary>
        /// Fires when a pen flow starts in automatic/manual mode.
        /// The boolean parameter indicates whether the odor is really flowing to the participant
        /// </summary>
        public event EventHandler<bool> OdorFlowStarted = delegate { };
        /// <summary>
        /// Inhale start event
        /// </summary>
        public event EventHandler InhaleStarts = delegate { };
        /// <summary>
        /// Fires when all pens were active, and it is time select the pen with odorant
        /// </summary>
        public event EventHandler<AnswerType> WaitingForAnswer = delegate { };
        /// <summary>
        /// Fires when the trial is finished and the next should follow, passes the trial result
        /// </summary>
        public event EventHandler<bool> Next = delegate { };
        /// <summary>
        /// Fires when all trials are finished, passes the test result
        /// </summary>
        public event EventHandler<double> Finished = delegate { };

        public int Step => _rules.Step + 1;
        public IProcState.PPMChangeDirection Direction => _rules.Direction;
        public double PPM => _rules.PPM;
        public int RecognitionsInRow => _rules.RecognitionsInRow;
        public int TurningPointCount => _rules.TurningPointCount;

        public Settings.FlowStartTrigger FlowStart => _settings.FlowStart;
        public bool CanChoose => _settings.Type == Settings.ProcedureType.OnePen;

        public int PenCount => _settings.Type switch
        {
            Settings.ProcedureType.ThreePens => 3,
            Settings.ProcedureType.TwoPens => 2,
            Settings.ProcedureType.OnePen => 1,
            _ => throw new NotImplementedException($"There are no pens in the procedure '{_settings.Type}'"),
        };

        public ProcedurePens()
        {
            // catch window closing event, so we do not display termination message due to MFC comm closed
            Application.Current.MainWindow.Closing += (s, e) =>
            {
                _inProgress = false;
                _pidTimer.Stop();
                _mfcTimer.Stop();
            };

            // If the test is in progress, display warning message and quit the app:
            // Or can we recover here by trying to open the COM port again?
            MFC.Instance.Closed += (s, e) =>
            {
                if (_inProgress)
                {
                    MsgBox.Error(
                        L10n.T("OlfactoryTestTool") + " - " + L10n.T("ThresholdTest"),
                        string.Format(L10n.T("DeviceConnLost"), "MFC") + " " + L10n.T("AppTerminated"));
                    Application.Current.Shutdown();
                }
            };

            _pidTimer.Elapsed += (s, e) => Dispatcher.CurrentDispatcher.Invoke(MeasurePID);
            _mfcTimer.Elapsed += (s, e) => Dispatcher.CurrentDispatcher.Invoke(MeasureMFC);

            _model.TargetOdorLevelReached += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("FL" + (e ? "0" : "1"));
            };
        }

        /// <summary>
        /// Starts a new trial
        /// </summary>
        /// <param name="settings">Settings</param>
        /// <returns>A set of pens</returns>
        public Pen[] Start(Settings settings)
        {
            if (settings != null)
            {
                _settings = settings;
                _odorTubeFillingDuration = settings.OdorPreparationDuration - OUTPUT_READINESS_DURATION;

                var pens = new Pen[PenCount];
                pens[0] = new Pen(PenColor.Odor);

                var ppms = _settings.PPMs;
                var recInRow = _settings.RecognitionsInRow;
                _rules = _settings.Type switch
                {
                    Settings.ProcedureType.OnePen => new TurningYesNo(ppms.First(), ppms.Last(), recInRow),
                    Settings.ProcedureType.TwoPens => new TurningForcedChoiceDynamic(ppms.First(), ppms.Last(), recInRow, 1),
                    Settings.ProcedureType.ThreePens => new TurningForcedChoice(ppms, recInRow),
                    _ => throw new NotImplementedException($"No rules are implemented for '{_settings.Type}' procedure type")
                };

                for (int i = 1; i < PenCount; i++)
                {
                    pens[i] = new Pen(PenColor.NonOdor);
                }

                _pens = pens.ToArray();
            }

            _currentPenID = -1;
            _inProgress = true;
            _isAwaitingOdorFlowStart = false;

            _rules.Next(_pens);

            _logger.Add(LogSource.ThTest, "trial", State);
            _logger.Add(LogSource.ThTest, "colors", string.Join(' ', _pens.Select(pen => pen.Color.ToString())));

            _pidTimer.Interval = _settings.PIDReadingInterval;
            _pidTimer.Start();

            _mfcTimer.Interval = TIMER_MFC_INTERVAL;
            _mfcTimer.Start();

            DispatchOnce.Do(0.5, () => PrepareOdor());  // the previous page finsihed with a command issued to MFC..
                                                        // lets wait a little just in case, then continue
            return _pens;
        }

        /// <summary>
        /// When wating for <see cref="AnswerType.HasOdor"/> answer: the selected pen
        /// When wating for <see cref="AnswerType.YesNo"/> answer: the pen if "yes" was selected, or null if "no" was selected
        /// </summary>
        /// <param name="pen">Selected pen, or no pen if no odor was perceived</param>
        public void Select(Pen pen)
        {
            var isCorrectChoice = pen == null
                ? _pens[0].Color == PenColor.NonOdor
                : pen.Color == PenColor.Odor;
            var canContinue = AdjustPPM(isCorrectChoice);

            _logger.Add(LogSource.ThTest, "result", isCorrectChoice.ToString());

            DispatchOnce.Do(AFTERMATH_PAUSE, () =>
            {
                if (!canContinue)    // there is no way to change the ppm anymore, exit
                {
                    var result = _rules.Result(_settings.TurningPointsToCount);
                    Finished(this, result);
                    Stop();
                }
                else
                {
                    Next(this, isCorrectChoice);
                }
            });
        }

        public void Stop()
        {
            _pidTimer.Stop();
            _mfcTimer.Stop();
            _inProgress = false;
            _isAwaitingOdorFlowStart = false;
        }

        /// <summary>
        /// Should be called from the parent to be notified when a user pressed SPACE/ENTER key
        /// </summary>
        public void EnablePenOdor()
        {
            if (_settings.FlowStart != Settings.FlowStartTrigger.Immediate && _isAwaitingOdorFlowStart)
            {
                _isAwaitingOdorFlowStart = false;
                StartOdorFlow();
            }
        }

        // ITestEmulation

        public void EmulationInit()
        {
            //_odorTubeFillingDuration = 10;
            //_settings.OdorPreparationDuration = 2;
            //_settings.PenSniffingDuration = 1;
        }

        public void EmulationFinilize()
        {
            while (_inProgress && _rules.TurningPointCount < _settings.TurningPoints)
            {
                _rules._SimulateTurningPoint(_settings.PPMs[6]);
            }
        }


        // Internal

        // Contants / readonlies

        const double AFTERMATH_PAUSE = 3;               // seconds
        const double OUTPUT_READINESS_DURATION = 5;     // seconds
        const double ODOR_PREPARATION_REPORT_INTERVAL = 0.2;    // seconds
        const int TIMER_MFC_INTERVAL = 1000;

        // Properties

        PenColor CurrentColor => _pens != null && (0 <= _currentPenID && _currentPenID < _pens.Length)
            ? _pens[_currentPenID].Color
            : PenColor.None;

        string[] State => new string[] {
            Step.ToString(),
            Direction.ToString(),
            PPM.ToString("F2"),
            RecognitionsInRow.ToString(),
            TurningPointCount.ToString()
        };


        // Members

        readonly OlfactoryDeviceModel _model = new();
        readonly BreathingDetector _breathingDetector = new();
        readonly SoundPlayer _waitingSounds = new(Properties.Resources.WaitingSound);
        readonly System.Timers.Timer _pidTimer = new();
        readonly System.Timers.Timer _mfcTimer = new();

        readonly FlowLogger _logger = FlowLogger.Instance;
        readonly PID _pid = PID.Instance;
        readonly MFC _mfc = MFC.Instance;
        readonly CommMonitor _monitor = CommMonitor.Instance;

        Pen[] _pens;

        Settings _settings = new();
        TurningBase _rules;

        bool _inProgress = false;
        int _currentPenID = -1;

        double _odorTubeFillingDuration = 10;

        double _odorPreparationStart = 0;
        bool _isAwaitingOdorFlowStart = false;

        /// <summary>
        /// Called by PID measurement timer
        /// </summary>
        private void MeasurePID()
        {
            if (_pid.GetSample(out PIDSample pidSample).Error == Error.Success)
            {
                _logger.Add(LogSource.PID, "data", pidSample.ToString());
                _monitor.LogData(LogSource.PID, pidSample);

                if (_settings.FlowStart == Settings.FlowStartTrigger.Automatic &&
                    _breathingDetector.Feed(pidSample.Time, pidSample.Loop) &&
                    _breathingDetector.BreathingStage == BreathingDetector.Stage.Inhale &&
                    _isAwaitingOdorFlowStart)
                {
                    _isAwaitingOdorFlowStart = false;
                    StartOdorFlow();
                }
            }
        }

        private void MeasureMFC()
        {
            if (_mfc.GetSample(out MFCSample mfcSample).Error == Error.Success)
            {
                _monitor.LogData(LogSource.MFC, mfcSample);
            }
        }

        /// <summary>
        /// Sets the MFC-B (odor tube) speed so that the odor fills the tube in 10 seconds,
        /// then sets the MFC-B speed to the level desired in this trial, and wait for another 5 seconds
        /// </summary>
        private void PrepareOdor()
        {
            if (_settings.UseFeedbackLoopToReachLevel)
            {
                _model.Reach(_rules.PPM, _settings.OdorPreparationDuration, _settings.UseFeedbackLoopToKeepLevel);
            }
            else
            {
                var readinessDelay = _model.GetReady(_rules.PPM, _odorTubeFillingDuration);

                // This is the way we react if readinessDelay > _settings.OdorPreparationDuration : show a warning and quit the app.
                if (readinessDelay > _settings.OdorPreparationDuration)
                {
                    MsgBox.Error(
                        L10n.T("OlfactoryTestTool") + " - " + L10n.T("ThresholdTest"),
                        string.Format(L10n.T("OdorFlowTooHigh"), _settings.OdorPreparationDuration) + " " + L10n.T("AppTerminated"));
                    Application.Current.Shutdown();
                    return;
                }
            }

            DispatchOnce.Do(_settings.OdorPreparationDuration, () => ActivateNextPen());

            _odorPreparationStart = Timestamp.Sec;
            DispatchOnce.Do(ODOR_PREPARATION_REPORT_INTERVAL, () => EstimatePreparationProgress() );
        }

        /// <summary>
        /// Prepare a pen to be recognized. 
        /// Controls MFC
        /// IF the pen has odor, then
        /// - sets the max speed to odor tube MFC (MFC-B)
        /// - after the odor fills the odor tube, sets the MFC-B speed to the desired speed
        /// - waits for 0.5s for the mixed air to stabilize
        /// - optionally, waits for user inhale
        /// - switches the valve #2 output (odor flows to the user)
        /// OTHERWISE
        /// - just wait same time,
        /// - then enables "Pen #N" button that finilizes this pen 
        /// </summary>
        private void ActivateNextPen()
        {
            if (CurrentColor == PenColor.Odor)     // previous pen was with the odor - switch the mixer back to the fresh air
            {
                _model.CloseFlow();
            }

            if (++_currentPenID == _pens.Length)
            {
                _currentPenID = -1;
                _logger.Add(LogSource.ThTest, "awaiting");

                WaitingForAnswer(this, _settings.Type == Settings.ProcedureType.OnePen ? AnswerType.YesNo : AnswerType.HasOdor);
                return;
            }

            _logger.Add(LogSource.ThTest, "pen", CurrentColor.ToString());

            switch (_settings.FlowStart)
            {
                case Settings.FlowStartTrigger.Immediate:
                    StartOdorFlow();
                    break;
                case Settings.FlowStartTrigger.Manual:
                    _isAwaitingOdorFlowStart = true;
                    break;
                case Settings.FlowStartTrigger.Automatic:
                    _isAwaitingOdorFlowStart = true;
                    break;
            }

            _waitingSounds.Play();

            PenActivated(this, new PenActivationArgs(_currentPenID, _settings.FlowStart));
        }

        /// <summary>
        /// For OnePen procedure type only:
        /// Estimates whether the next trial has odored air
        /// </summary>
        /// <returns>'True' if odored</returns>
        private bool IsNextTrialOdored()
        {
            // TODO
            return true;
        }

        private void StartOdorFlow()
        {
            if (CurrentColor == PenColor.Odor)
            {
                _model.OpenFlow();
            }

            OdorFlowStarted(this, CurrentColor == PenColor.Odor);

            DispatchOnce.Do(_settings.PenSniffingDuration, () => ActivateNextPen());
        }

        private bool AdjustPPM(bool odorWasRecognized)
        {
            if(!_rules.AcceptAnswer(odorWasRecognized))
            {
                return false;
            }

            if (_rules.TurningPointCount >= _settings.TurningPoints)
            {
                return false;
            }

            return true;
        }

        private void EstimatePreparationProgress()
        {
            if (_currentPenID < 0)
            {
                var duration = Timestamp.Sec - _odorPreparationStart;
                var progress = Math.Min(1.0, duration / _settings.OdorPreparationDuration);
                OdorPreparation(this, progress);

                if (progress < 1)
                {
                    DispatchOnce.Do(ODOR_PREPARATION_REPORT_INTERVAL, () => EstimatePreparationProgress());
                }
            }
        }
    }
}
