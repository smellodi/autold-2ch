﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Olfactory.Comm;
using Olfactory.Utils;

namespace Olfactory.Tests.ThresholdTest
{
    public class Procedure : ITestEmulator
    {
        public const int PEN_COUNT = 3;
        public enum PPMChangeDirection { Increasing, Decreasing }

        /// <summary>
        /// Fires when some pen is activated, passes its ID
        /// </summary>
        public event EventHandler<int> PenActivated = delegate { };
        /// <summary>
        /// Fires when all pens were active, and it is time select the pen with odorant
        /// </summary>
        public event EventHandler WaitingForPenSelection = delegate { };
        /// <summary>
        /// Fires when the trial is finished and the next should follow, passes the trial result
        /// </summary>
        public event EventHandler<bool> Next = delegate { };
        /// <summary>
        /// Fires when all trials are finished, passes the test result
        /// </summary>
        public event EventHandler<double> Finished = delegate { };

        public int Step => _stepID + 1;
        public PPMChangeDirection Direction => _direction;
        public int PPMLevel => _currentPPMLevel + 1;
        public int RecognitionsInRow => _recognitionsInRow;
        public int TurningPointCount => _turningPointPPMs.Count;

        private string[] State => new string[] {
            Step.ToString(),
            Direction.ToString(),
            PPMLevel.ToString(),
            RecognitionsInRow.ToString(),
            TurningPointCount.ToString()
        };

        public Procedure()
        {
            // catch window closing event, so we do not display termination message due to MFC comm closed
            Application.Current.MainWindow.Closing += (s, e) =>
            {
                _inProgress = false;
                _timer.Stop();
            };

            // If the test is in progress, display warning message and quit the app:
            // Or can we recover here by trying to open the COM port again?
            MFC.Instance.Closed += (s, e) =>
            {
                if (_inProgress)
                {
                    MessageBox.Show("Connection with the MFC device was shut down. The application is terminated.",
                        Application.Current.MainWindow.Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
            };

            _timer.Elapsed += (s, e) => Dispatcher.CurrentDispatcher.Invoke(Measure);

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
            }

            _currentPenID = -1;
            _inProgress = true;

            _pens = new Pen[PEN_COUNT] {
                new Pen(PenColor.Red),
                new Pen(PenColor.Green),
                new Pen(PenColor.Blue)
            };
            _rnd.Shuffle(_pens);

            _logger.Add(LogSource.ThTest, "trial", State);
            _logger.Add(LogSource.ThTest, "order", string.Join(' ', _pens.Select(pen => pen.Color.ToString())));

            _timer.Interval = _settings.PIDReadingInterval;
            _timer.Start();

            _stepID++;

            DispatchOnce.Do(0.5, () => PrepareOdor());  // the previous page finsihed with a command issued to MFC..
                                                        // lets wait a little just in case, then continue
            return _pens;
        }

        public void Select(Pen pen)
        {
            var isCorrectChoice = pen.Color == ODOR_PEN_COLOR;
            var canContinue = AdjustPPM(isCorrectChoice);

            _logger.Add(LogSource.ThTest, "result", isCorrectChoice.ToString());

            DispatchOnce.Do(AFTERMATH_PAUSE, () =>
            {
                if (!canContinue)    // there is no way to change the ppm anymore, exit
                {
                    var result = _currentPPMLevel < 0 ? -1 : _turningPointPPMs.TakeLast(_settings.TurningPointsToCount).Average();
                    Finished(this, result);
                    _inProgress = false;
                    _timer.Stop();
                }
                else
                {
                    Next(this, isCorrectChoice);
                }
            });
        }

        public void Interrupt()
        {
            _timer.Stop();
            _inProgress = false;
        }

        public void Measure()
        {
            if (_pid.GetSample(out PIDSample pidSample).Error == Error.Success)
            {
                _logger.Add(LogSource.PID, "data", pidSample.ToString());
                _monitor.LogData(LogSource.PID, pidSample);
            }
            if (_mfc.GetSample(out MFCSample mfcSample).Error == Error.Success)
            {
                _monitor.LogData(LogSource.MFC, mfcSample);
            }
        }

        // ITestEmulation

        public void EmulationInit()
        {
            _odorTubeFillingDuration = 10;
            _settings.OdorPreparationDuration = 2;
            _settings.PenSniffingDuration = 1;
        }

        public void EmulationFinilize()
        {
            while (_inProgress && _turningPointPPMs.Count < _settings.TurningPoints)
            {
                _turningPointPPMs.Add(_settings.PPMs[6]);
            }
        }


        // Internal

        // Contants / readonlies

        const int PPM_LEVEL_STEP = 1;
        const double AFTERMATH_PAUSE = 3;           // seconds
        const double OUTPUT_READINESS_DURATION = 5; // seconds

        const PenColor ODOR_PEN_COLOR = PenColor.Red;

        // Properties

        PenColor CurrentColor => _pens != null && (0 <= _currentPenID && _currentPenID < _pens.Length)
            ? _pens[_currentPenID].Color
            : PenColor.None;


        // Members

        OlfactoryDeviceModel _model = new OlfactoryDeviceModel();
        FlowLogger _logger = FlowLogger.Instance;
        PID _pid = PID.Instance;
        MFC _mfc = MFC.Instance;
        Settings _settings = new Settings();
        CommMonitor _monitor = CommMonitor.Instance;

        System.Timers.Timer _timer = new System.Timers.Timer();

        bool _inProgress = false;

        Random _rnd = new Random((int)DateTime.Now.Ticks);

        Pen[] _pens;
        int _currentPenID = -1;

        int _stepID = -1;
        int _currentPPMLevel = 0;

        PPMChangeDirection _direction = PPMChangeDirection.Increasing;
        int _recognitionsInRow = 0;
        List<double> _turningPointPPMs = new List<double>();

        double _odorTubeFillingDuration = 10;

        /// <summary>
        /// Sets the MFC-B (odor tube) speed so that the odor fills the tube in 10 seconds,
        /// then sets the MFC-B speed to the level desired in this trial, and wait for another 5 seconds
        /// </summary>
        private void PrepareOdor()
        {
            if (_settings.UseFeedbackLoopToReachLevel)
            {
                _model.Reach(_settings.PPMs[_currentPPMLevel], _settings.OdorPreparationDuration, _settings.UseFeedbackLoopToKeepLevel);

                DispatchOnce.Do(_settings.OdorPreparationDuration, () => ActivateNextPen());
            }
            else
            {
                var readinessDelay = _model.GetReady(_settings.PPMs[_currentPPMLevel], _odorTubeFillingDuration);

                // This is the way we react if readinessDelay > _settings.OdorPreparationDuration : show a warning and quit the app.
                if (readinessDelay > _settings.OdorPreparationDuration)
                {
                    MessageBox.Show($"The odor flow rate is too high to be ready in {_settings.OdorPreparationDuration} seconds. The application is terminated.",
                            Application.Current.MainWindow.Title,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
                else
                {
                    DispatchOnce.Do(_settings.OdorPreparationDuration, () => ActivateNextPen());
                }
            }
        }

        /// <summary>
        /// Prepare a pen to be recognized. 
        /// Controls MFC
        /// IF the pen has odor, then
        /// - sets the max speed to odor tube MFC (MFC-B)
        /// - after the odor fills the odor tube, sets the MFC-B speed to the desired speed
        /// - waits for 0.5s for the mixed air to stabilize
        /// - switches the output (odor goed to the user) and enables "Pen #N" button that finilizes this pen 
        /// OTHERWISE
        /// - just wait same time,
        /// - then enable "Pen #N" button that finilizes this pen 
        /// </summary>
        private void ActivateNextPen()
        {
            if (CurrentColor == ODOR_PEN_COLOR)     // previous pen was with the odor - switch the mixer back to the fresh air
            {
                _model.CloseFlow();
            }

            if (++_currentPenID == _pens.Length)
            {
                _currentPenID = -1;
                _logger.Add(LogSource.ThTest, "awaiting");

                WaitingForPenSelection(this, new EventArgs());
                return;
            }

            _logger.Add(LogSource.ThTest, "pen", CurrentColor.ToString());

            if (CurrentColor == ODOR_PEN_COLOR)
            {
                _model.OpenFlow();
            }

            DispatchOnce.Do(_settings.PenSniffingDuration, () => ActivateNextPen());

            PenActivated(this, _currentPenID);
        }

        private void UpdatePPMLevelAndDirection(int ppmLevelChange, PPMChangeDirection direction)
        {
            if (_direction != direction)
            {
                _turningPointPPMs.Add(_settings.PPMs[_currentPPMLevel]);
                _direction = direction;
            }

            _recognitionsInRow = 0;
            _currentPPMLevel += ppmLevelChange;
        }

        private bool AdjustPPM(bool odorWasRecognized)
        {
            if (!odorWasRecognized)
            {
                UpdatePPMLevelAndDirection(
                    _turningPointPPMs.Count == 0 ? 2 * PPM_LEVEL_STEP : PPM_LEVEL_STEP, // increase with x2 step before the first turning point
                    PPMChangeDirection.Increasing
                    );
            }
            else if (++_recognitionsInRow == _settings.RecognitionsInRow)               // decrease ppm only if recognized correctly few times in a row
            {
                UpdatePPMLevelAndDirection(
                    -PPM_LEVEL_STEP,
                    PPMChangeDirection.Decreasing
                    );
            }

            bool isOverflow = _currentPPMLevel < 0 || _settings.PPMs.Length <= _currentPPMLevel;

            if (_turningPointPPMs.Count >= _settings.TurningPoints)
            {
                return false;
            }
            else if (isOverflow)
            {
                _currentPPMLevel = -1;
                return false;
            }

            return true;
        }

    }
}
