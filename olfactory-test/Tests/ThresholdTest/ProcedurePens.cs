using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Olfactory.Comm;
using Olfactory.Utils;

namespace Olfactory.Tests.ThresholdTest
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

    public class ProcedurePens : ITestEmulator
    {
        /// <summary>
        /// Fires on progressing the odor preparation
        /// </summary>
        public event EventHandler<double> OdorPreparation;
        /// <summary>
        /// Fires when some pen is activated, passes its ID
        /// </summary>
        public event EventHandler<PenActivationArgs> PenActivated;
        /// <summary>
        /// Fires when a pen flow starts in automatic/manual mode.
        /// The boolean parameter indicates whether the odor is really flowing to the participant
        /// </summary>
        public event EventHandler<bool> OdorFlowStarted;
        /// <summary>
        /// Fires when all pens were active, and it is time select the pen with odorant
        /// </summary>
        public event EventHandler<AnswerType> WaitingForAnswer;
        /// <summary>
        /// Fires when the trial is finished and the next should follow, passes the trial result
        /// </summary>
        public event EventHandler<bool> Next;
        /// <summary>
        /// Fires when all trials are finished, passes the test result
        /// </summary>
        public event EventHandler<double> Finished;

        public int Step => _rules.Step + 1;
        public IProcState.PPMChangeDirection Direction => _rules.Direction;
        public double PPM => _rules.PPM;
        public int RecognitionsInRow => _rules.RecognitionsInRow;
        public int TurningPointCount => _rules.TurningPointCount;

        public Settings.FlowStartTrigger FlowStart => _settings.FlowStart;
        public bool CanChoose => _settings.Type == Settings.ProcedureType.OnePen;

        public BreathingDetector BreathingDetector => _breathingDetector;

        public int PenCount => _settings.Type switch
        {
            Settings.ProcedureType.ThreePens => 3,
            Settings.ProcedureType.TwoPens => 2,
            Settings.ProcedureType.OnePen => 1,
            _ => throw new NotImplementedException($"There are no pens in the procedure '{_settings.Type}'"),
        };

        public ProcedurePens(bool isPracticing)
        {
            _isPracticing = isPracticing;

            _pidTimer.Elapsed += (s, e) => Dispatcher.CurrentDispatcher.Invoke(MeasurePID);

            if (!_isPracticing)
            {
                _mfcTimer.Elapsed += (s, e) => Dispatcher.CurrentDispatcher.Invoke(MeasureMFC);

                _model.TargetOdorLevelReached += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("FL" + (e ? "0" : "1"));
                };
            }
            else
            {
                _mfc = null;
                _model = null;
            }
        }

        /// <summary>
        /// Initializes the procedure. This must be called before starting trials
        /// </summary>
        /// <param name="settings">Settings</param>
        public void Init(Settings settings)
        {
            _settings = settings;
            _odorTubeFillingDuration = settings.OdorPreparationDuration - OUTPUT_READINESS_DURATION;

            var ppms = _settings.PPMs;
            var recInRow = _settings.RecognitionsInRow;
            _rules = _settings.Type switch
            {
                Settings.ProcedureType.OnePen => new TurningYesNo(ppms.First(), ppms.Last(), recInRow),
                Settings.ProcedureType.TwoPens => new TurningForcedChoiceDynamic(ppms.First(), ppms.Last(), recInRow, 1),
                Settings.ProcedureType.ThreePens => new TurningForcedChoice(ppms, recInRow),
                _ => throw new NotImplementedException($"No rules are implemented for '{_settings.Type}' procedure type")
            };

            _pens = new Pen[PenCount];
            _pens[0] = new Pen(PenColor.Odor);

            for (int i = 1; i < PenCount; i++)
            {
                _pens[i] = new Pen(PenColor.NonOdor);
            }

            // catch window closing event, so we do not display termination message due to MFC comm closed
            Application.Current.MainWindow.Closing += MainWindow_Closing;

            // If the test is in progress, display warning message and quit the app:
            // Or can we recover here by trying to open the COM port again?
            MFC.Instance.Closed += MFC_Closed;
        }

        /// <summary>
        /// Starts a new trial
        /// </summary>
        /// <returns>A set of pens</returns>
        public Pen[] StartTrial()
        {
            _currentPenID = -1;
            _inProgress = true;
            _isAwaitingOdorFlowStart = false;

            _rules.Next(_pens);

            if (_isPracticing)
            {
                _logger.Add(LogSource.ThTest, "practice", "start");
            }
            else
            { 
                _mfcTimer.Interval = 1000 * MFC_READING_INTERVAL;
                _mfcTimer.Start();
            }

            _logger.Add(LogSource.ThTest, "trial", State);
            _logger.Add(LogSource.ThTest, "colors", string.Join(' ', _pens.Select(pen => pen.Color.ToString())));

            _monitor.MFCUpdateInterval = MFC_READING_INTERVAL;
            _monitor.PIDUpdateInterval = 0.001 * _settings.PIDReadingInterval;

            _pidTimer.Interval = _settings.PIDReadingInterval;
            _pidTimer.Start();

            DispatchOnce.Do(0.5, () => PrepareOdor());  // the previous page finsihed with a command issued to MFC..
                                                        // lets wait a little just in case, then continue
            return _pens;
        }

        /// <summary>
        /// Reacts to user answer.
        /// </summary>
        /// <param name="pen">A user answer:
        ///     the selected pen (when waiting for <see cref="AnswerType.HasOdor"/> answer),
        ///     the pen if "yes" was selected, or null if "no" was selected (when wating for <see cref="AnswerType.YesNo"/> answer)
        /// </param>
        public void Select(Pen pen)
        {
            var isCorrectChoice = pen == null
                ? _pens[0].Color == PenColor.NonOdor
                : pen.Color == PenColor.Odor;

            var canContinue = CanContinueTest(isCorrectChoice);

            _logger.Add(LogSource.ThTest, "result", isCorrectChoice.ToString());

            DispatchOnce.Do(AFTERMATH_PAUSE, () =>
            {
                if (!canContinue)    // there is no way to change the ppm anymore, exit
                {
                    if (_settings.UseValveTimer)
                    {
                        _model.Finilize();
                    }

                    var result = _rules.Result(_settings.TurningPointsToCount);
                    Finished?.Invoke(this, result);
                    Stop();

                    if (_isPracticing)
                    {
                        _logger.Add(LogSource.ThTest, "practice", "end");
                    }
                }
                else
                {
                    Next?.Invoke(this, isCorrectChoice);
                }
            });
        }

        public void Stop()
        {
            if (!_isPracticing)
            {
                _mfcTimer.Stop();
            }

            _pidTimer.Stop();
            _inProgress = false;
            _isAwaitingOdorFlowStart = false;

            Application.Current.MainWindow.Closing -= MainWindow_Closing;
            MFC.Instance.Closed -= MFC_Closed;
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

        const double MIN_PEN_PRESENTATION_TIME = 3;     // seconds
        const double AFTERMATH_PAUSE = 3;               // seconds
        const double OUTPUT_READINESS_DURATION = 5;     // seconds
        const double ODOR_PREPARATION_REPORT_INTERVAL = 0.2;    // seconds
        const double MFC_READING_INTERVAL = 1;             // seconds

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

        readonly static SoundPlayer _waitingSounds = new(Properties.Resources.WaitingSound);

        readonly OlfactoryDeviceModel _model = new();
        readonly BreathingDetector _breathingDetector = new();
        readonly System.Timers.Timer _pidTimer = new();
        readonly System.Timers.Timer _mfcTimer = new();

        readonly FlowLogger _logger = FlowLogger.Instance;
        readonly PID _pid = PID.Instance;
        readonly MFC _mfc = MFC.Instance;
        readonly CommMonitor _monitor = CommMonitor.Instance;

        readonly bool _isPracticing = false;

        Pen[] _pens;

        Settings _settings = new();
        TurningBase _rules;

        bool _inProgress = false;
        int _currentPenID = -1;

        double _odorTubeFillingDuration = 10;

        double _odorPreparationStart = 0;
        bool _isAwaitingOdorFlowStart = false;
        bool _isAwaitingNextPen = false;

        /// <summary>
        /// Called by PID measurement timer
        /// </summary>
        private void MeasurePID()
        {
            if (_pid.GetSample(out PIDSample pidSample).Error == Error.Success)
            {
                if (!_isPracticing)
                {
                    _logger.Add(LogSource.PID, "data", pidSample.ToString());
                }

                _monitor.LogData(LogSource.PID, pidSample);

                if (_settings.FlowStart == Settings.FlowStartTrigger.Automatic &&
                    _breathingDetector.Feed(pidSample.Time, pidSample.Loop))
                {
                    if (_breathingDetector.BreathingStage == BreathingDetector.Stage.Inhale && _isAwaitingOdorFlowStart)
                    {
                        _isAwaitingOdorFlowStart = false;
                        StartOdorFlow();
                    }
                    else if (_breathingDetector.BreathingStage == BreathingDetector.Stage.Exhale && _isAwaitingNextPen)
                    {
                        _isAwaitingNextPen = false;
                        ActivateNextPen();
                    }
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
            if (!_isPracticing)
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
            }

            DispatchOnce.Do(_settings.OdorPreparationDuration, () => ActivateNextPen());

            _odorPreparationStart = Timestamp.Sec;
            DispatchOnce.Do(ODOR_PREPARATION_REPORT_INTERVAL, () => EstimatePreparationProgress() );
        }

        private void StopOdorFlow()
        {
            if (!_isPracticing && CurrentColor == PenColor.Odor && _mfc.OdorDirection.HasFlag(MFC.OdorFlowsTo.User))     // previous pen was with the odor - switch the mixer back to the fresh air
            {
                _model.CloseFlow();
            }
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
            StopOdorFlow();

            if (++_currentPenID == _pens.Length)
            {
                _currentPenID = -1;
                _logger.Add(LogSource.ThTest, "awaiting");

                WaitingForAnswer?.Invoke(this, _settings.Type == Settings.ProcedureType.OnePen ? AnswerType.YesNo : AnswerType.HasOdor);
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

            PenActivated?.Invoke(this, new PenActivationArgs(_currentPenID, _settings.FlowStart));
        }

        private void StartOdorFlow()
        {
            if (!_isPracticing && CurrentColor == PenColor.Odor)
            {
                _model.OpenFlow(_settings.UseValveTimer ? _settings.PenSniffingDuration : 0);
            }

            OdorFlowStarted?.Invoke(this, CurrentColor == PenColor.Odor);

            if (_settings.FlowStart == Settings.FlowStartTrigger.Automatic)
            {
                DispatchOnce.Do(_settings.PenSniffingDuration, () =>
                {
                    StopOdorFlow();
                    _isAwaitingNextPen = true;
                });
            }
            else
            {
                var penPresentationTime = Math.Max(_settings.PenSniffingDuration, MIN_PEN_PRESENTATION_TIME);
                DispatchOnce.Do(penPresentationTime, ActivateNextPen);

                if (_settings.PenSniffingDuration > penPresentationTime)
                {
                    DispatchOnce.Do(_settings.PenSniffingDuration, StopOdorFlow);
                }
            }
        }

        /// <summary>
        /// Acceprs user answer and returns the test continuation flag
        /// </summary>
        /// <param name="odorWasRecognized">whether a user correctly recognized the presense/absence of odor</param>
        /// <returns>'True' is the test must be continued with another trial, 'False' if the test must be finished</returns>
        private bool CanContinueTest(bool odorWasRecognized)
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
                OdorPreparation?.Invoke(this, progress);

                if (progress < 1)
                {
                    DispatchOnce.Do(ODOR_PREPARATION_REPORT_INTERVAL, () => EstimatePreparationProgress());
                }
            }
        }

        // Event handlers

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _inProgress = false;
            _pidTimer.Stop();

            if (!_isPracticing)
            {
                _mfcTimer.Stop();
            }
        }

        private void MFC_Closed(object sender, EventArgs e)
        {
            if (_inProgress)
            {
                MsgBox.Error(
                    L10n.T("OlfactoryTestTool") + " - " + L10n.T("ThresholdTest"),
                    string.Format(L10n.T("DeviceConnLost"), "MFC") + " " + L10n.T("AppTerminated"));
                Application.Current.Shutdown();
            }
        }
    }
}
