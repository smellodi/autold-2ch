using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Utils;
using PenColor = Olfactory.Tests.ThresholdTest.PenColor;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class ThreePens : Page, IPage<bool>
    {
        public event EventHandler<bool> Next = delegate { };
        public event EventHandler<double> Finished = delegate { };


        public ThreePens()
        {
            InitializeComponent();

            PENS = new Controls.Pen[PEN_COUNT] { pen1, pen2, pen3 };
        }

        public void Init()
        {
            _currentPenID = -1;

            var order = new PenColor[PEN_COUNT] {
                PenColor.Red,
                PenColor.Green,
                PenColor.Blue
            };
            _rnd.Shuffle(order);
            _stepID++;

            for (int i = 0; i < PENS.Length; i++)
            {
                PENS[i].PenInstance = new Tests.ThresholdTest.Pen(order[i]);
            }

            System.Diagnostics.Debug.WriteLine($"{order[0]} {order[1]} {order[2]}");

            UpdateDisplay(Update.All);

            lblInstruction.Text = INSTRUCTION_WAIT_FOR_THE_TRIAL_TO_START;
            DispatchOnce.Do(0.5, () => PrepareOdor());  // the previous page finsihed with a command issued to MFC..
                                                        // lets wait a little just in case, then continue
        }


        // Internal

        // Contants

        const int PEN_COUNT = 3;
        const int PPM_LEVEL_COUNT = 16;
        const int PPM_LEVEL_STEP = 1;
        const int RECOGNITIONS_IN_ROW_COUNT = 2;
        const int TURNING_POINT_COUNT = 7;
        const int TURNING_POINTS_TO_USE_IN_ESTIMATION = 4;

        // the sum of these 2 durations must be 15 seconds
        const double ODOR_TUBE_FILLING_DURATION = 10;
        const double ODOR_STABILIZATION_DURATION = 5;

        const double PEN_PRESENTATION_DURATION = 5;
        const double AFTERMATH_PAUSE = 3;

        string INSTRUCTION_SNIFF_THE_PEN(int id) => $"Sniff the pen #{id + 1}";
        const string INSTRUCTION_WAIT_FOR_THE_TRIAL_TO_START = "Please wait until the odorant in ready (approx. 15 seconds).";
        const string INSTRUCTION_CHOOSE_THE_PEN = "Please select the pen with the odorant.";
        const string INSTRUCTION_DONE = "Thanks, your choice has been recorded.";

        const PenColor ODOR_PEN_COLOR = PenColor.Red;

        // Definitions

        enum PPMChangeDirection { Increasing, Decreasing }
        [Flags]
        enum Update { Step = 1, PPM = 2, Recognitions = 4, Turnings = 8, All = Step | PPM | Recognitions | Turnings }

        /* readonly PenColor[][] ORDER_MATRIX = new PenColor[][]
        {
            new PenColor[PEN_COUNT] { PenColor.Red, PenColor.Green, PenColor.Blue },
            new PenColor[PEN_COUNT] { PenColor.Blue, PenColor.Red, PenColor.Green },
            new PenColor[PEN_COUNT] { PenColor.Green, PenColor.Blue, PenColor.Red },
        };*/

        readonly double[] PPMS = new double[PPM_LEVEL_COUNT] {
            1, 1.5, 2, 3, 4, 6, 8, 11, 14, 17, 22, 27, 33, 40, 48, 59
        };
        readonly Controls.Pen[] PENS;

        // Members

        MFC _mfc = MFC.Instance;

        Random _rnd = new Random((int)DateTime.Now.Ticks);

        int _stepID = -1;
        int _currentPenID = -1;
        int _currentPPMLevel = 0;

        PPMChangeDirection _direction = PPMChangeDirection.Increasing;
        int _recognitionsInRowCount = 0;
        List<double> _turningPointPPMs = new List<double>();

        Controls.Pen CurrentPen => (0 <= _currentPenID && _currentPenID < PENS.Length) ? PENS[_currentPenID] : null;
        PenColor CurrentColor => CurrentPen == null ? PenColor.None : CurrentPen.PenInstance.Color;


        /// <summary>
        /// Sets the MFC-B (odor tube) speed so that the odor fills the tube in 10 seconds,
        /// then sets the MFC-B speed to the level desired in this trial, and wait for another 5 seconds
        /// </summary>
        private void PrepareOdor()
        {
            var odorSpeed = _mfc.PredictFlowSpeed(ODOR_TUBE_FILLING_DURATION);

            _mfc.OdorSpeed = Math.Round(odorSpeed, 1);

            DispatchOnce
                .Do(ODOR_TUBE_FILLING_DURATION, () => _mfc.OdorSpeed = _mfc.PPM2Speed(PPMS[_currentPPMLevel]))
                .Then(ODOR_STABILIZATION_DURATION, () => ActivateNextPen());
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
            if (CurrentColor == ODOR_PEN_COLOR)                     // previous pen was with the odor - switch the mixer back to the fresh air
            {
                _mfc.OdorDirection = MFC.OdorFlow.ToWaste;

                // no need to wait till the trial is over, just stop odor flow at this point already
                DispatchOnce.Do(0.5, () => _mfc.OdorSpeed = MFC.ODOR_MIN_SPEED);  // delay 0.5 sec. just in case
            }
            if (CurrentPen != null)
            {
                CurrentPen.IsActive = false;
            }

            if (++_currentPenID == PENS.Length)
            {
                _currentPenID = -1;

                EnableChoiceButtons(true);
                return;
            }

            lblInstruction.Text = INSTRUCTION_SNIFF_THE_PEN(_currentPenID);

            if (CurrentColor == ODOR_PEN_COLOR)
            {
                _mfc.OdorDirection = MFC.OdorFlow.ToUser;
            }

            CurrentPen.IsActive = true;

            DispatchOnce.Do(PEN_PRESENTATION_DURATION, () => ActivateNextPen());
        }

        private void UpdatePPMLevelAndDirection(int ppmLevelChange, PPMChangeDirection direction)
        {
            if (_direction != direction)
            {
                _turningPointPPMs.Add(PPMS[_currentPPMLevel]);
                _direction = direction;
            }

            _recognitionsInRowCount = 0;
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
            else if (++_recognitionsInRowCount == RECOGNITIONS_IN_ROW_COUNT)               // decrease ppm only if recognized correctly wice in a row
            {
                UpdatePPMLevelAndDirection(
                    -PPM_LEVEL_STEP,
                    PPMChangeDirection.Decreasing
                    );
            }

            bool isOverflow = _currentPPMLevel < 0 || PPM_LEVEL_COUNT <= _currentPPMLevel;

            if (_turningPointPPMs.Count == TURNING_POINT_COUNT)
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


        // UI actions

        private void EnableChoiceButtons(bool enable)
        {
            lblInstruction.Text = enable ? INSTRUCTION_CHOOSE_THE_PEN : INSTRUCTION_DONE;

            foreach (var pen in PENS)
            {
                pen.IsSelectable = enable;
            }
        }

        private void UpdateDisplay(Update update)
        {
            if (update.HasFlag(Update.PPM))
            {
                lblDirection.Content = _direction;
                lblPPMLevel.Content = _currentPPMLevel + 1;
            }
            if (update.HasFlag(Update.Recognitions))
            {
                lblRecognitionsInRow.Content = _recognitionsInRowCount;
            }
            if (update.HasFlag(Update.Step))
            {
                lblStep.Content = _stepID + 1;
            }
            if (update.HasFlag(Update.Turnings))
            {
                lblTurningCount.Content = _turningPointPPMs.Count;
            }
        }


        // UI events

        private void OnPen_Selected(object sender, EventArgs e)
        {
            void ColorizePens(bool colorize)
            {
                foreach (var pen in PENS)
                {
                    pen.IsColorVisible = colorize;
                }
            }

            EnableChoiceButtons(false);

            var pen = sender as Controls.Pen;
            var isCorrectChoice = pen.PenInstance.Color == ODOR_PEN_COLOR;
            var canContinue = AdjustPPM(isCorrectChoice);

            ColorizePens(true);

            UpdateDisplay(Update.Recognitions | Update.Turnings);

            DispatchOnce.Do(AFTERMATH_PAUSE, () =>
            {
                ColorizePens(false);

                if (!canContinue)    // there is no way to change the ppm anymore, exit
                {
                    var result = _currentPPMLevel < 0 ? -1 : _turningPointPPMs.TakeLast(TURNING_POINTS_TO_USE_IN_ESTIMATION).Average();
                    Finished(this, result);
                }
                else
                {
                    Next(this, isCorrectChoice);
                }
            });

        }
    }
}
