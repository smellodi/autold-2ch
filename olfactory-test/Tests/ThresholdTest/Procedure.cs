using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Olfactory.Utils;
using OdorController = Olfactory.Comm.OdorController;

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
            };

            // If the test is in progress, display warning message and quit the app:
            // Or can we recover here by trying to open the COM port again?
            Comm.MFC.Instance.Closed += (s, e) =>
            {
                if (_inProgress)
                {
                    MessageBox.Show("Connection with the MFC device was shut down. The application is terminated.");
                    Application.Current.Shutdown();
                }
            };
        }

        /// <summary>
        /// Starts a new trial
        /// </summary>
        /// <returns>A set of pens</returns>
        public Pen[] Start()
        {
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
                    var result = _currentPPMLevel < 0 ? -1 : _turningPointPPMs.TakeLast(TURNING_POINTS_TO_USE_IN_ESTIMATION).Average();
                    Finished(this, result);
                    _inProgress = false;
                }
                else
                {
                    Next(this, isCorrectChoice);
                }
            });
        }

        // ITestEmulation

        public void EmulationInit()
        {
            ODOR_TUBE_FILLING_DURATION = 1;
            ODOR_PREPARATION_DURATION = 2;
            PEN_PRESENTATION_DURATION = 1;
            AFTERMATH_PAUSE = 1;
        }

        public void EmulationFinilize()
        {
            while (_inProgress && _turningPointPPMs.Count < TURNING_POINT_COUNT)
            {
                _turningPointPPMs.Add(PPMS[6]);
            }
        }


        // Internal

        // Contants / readonlies

        const int PPM_LEVEL_COUNT = 16;
        const int PPM_LEVEL_STEP = 1;
        const int RECOGNITIONS_IN_ROW_COUNT = 2;
        const int TURNING_POINT_COUNT = 7;
        const int TURNING_POINTS_TO_USE_IN_ESTIMATION = 4;

        // Next for value are not constants for emulation purposes.
        double ODOR_TUBE_FILLING_DURATION = 10;
        double ODOR_PREPARATION_DURATION = 15;      // Must be greater than ODOR_TUBE_FILLING_DURATION

        double PEN_PRESENTATION_DURATION = 5;
        double AFTERMATH_PAUSE = 3;

        const PenColor ODOR_PEN_COLOR = PenColor.Red;

        readonly double[] PPMS = new double[PPM_LEVEL_COUNT] {
            1, 1.5, 2, 3, 4, 6, 8, 11, 14, 17, 22, 27, 33, 40, 48, 59
        };


        // Properties

        PenColor CurrentColor => _pens != null && (0 <= _currentPenID && _currentPenID < _pens.Length)
            ? _pens[_currentPenID].Color
            : PenColor.None;


        // Members

        OdorController _odorController = OdorController.Instance;
        Logger _logger = Logger.Instance;

        bool _inProgress = false;

        Random _rnd = new Random((int)DateTime.Now.Ticks);

        Pen[] _pens;
        int _currentPenID = -1;

        int _stepID = -1;
        int _currentPPMLevel = 0;

        PPMChangeDirection _direction = PPMChangeDirection.Increasing;
        int _recognitionsInRow = 0;
        List<double> _turningPointPPMs = new List<double>();

        /// <summary>
        /// Sets the MFC-B (odor tube) speed so that the odor fills the tube in 10 seconds,
        /// then sets the MFC-B speed to the level desired in this trial, and wait for another 5 seconds
        /// </summary>
        private void PrepareOdor()
        {
            _odorController.GetReady(ODOR_TUBE_FILLING_DURATION, PPMS[_currentPPMLevel]);

            DispatchOnce.Do(ODOR_PREPARATION_DURATION, () => ActivateNextPen());
        }

        /// <summary>
        /// Prepared a pen to be recognized. 
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
                _odorController.CloseFlow();
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
                _odorController.OpenFlow();
            }

            DispatchOnce.Do(PEN_PRESENTATION_DURATION, () => ActivateNextPen());

            PenActivated(this, _currentPenID);
        }

        private void UpdatePPMLevelAndDirection(int ppmLevelChange, PPMChangeDirection direction)
        {
            if (_direction != direction)
            {
                _turningPointPPMs.Add(PPMS[_currentPPMLevel]);
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
            else if (++_recognitionsInRow == RECOGNITIONS_IN_ROW_COUNT)               // decrease ppm only if recognized correctly wice in a row
            {
                UpdatePPMLevelAndDirection(
                    -PPM_LEVEL_STEP,
                    PPMChangeDirection.Decreasing
                    );
            }

            bool isOverflow = _currentPPMLevel < 0 || PPM_LEVEL_COUNT <= _currentPPMLevel;

            if (_turningPointPPMs.Count >= TURNING_POINT_COUNT)
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
