using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class ThreePens : Page, IPage<bool>
    {
        public event EventHandler<bool> Next = delegate { };
        public event EventHandler<double> Finished = delegate { };

        // Contants

        const int PEN_COUNT = 3;
        const int PPM_LEVEL_COUNT = 16;
        const int PPM_LEVEL_STEP = 1;
        const int RECOGNITION_COUNT = 2;
        const int TURNING_POINT_COUNT = 7;
        const int TURNING_POINTS_TO_USE_IN_ESTIMATION = 4;
        const string INSTRUCTION_CLICK = "Click the pen button to move to the next pen";
        const string INSTRUCTION_WAIT = "";

        // Definitions

        enum PenColor { None, Red, Green, Blue }
        enum PPMChangeDirection { Increasing, Decreasing }

        readonly PenColor[][] ORDER_MATRIX = new PenColor[][]
        {
            new PenColor[PEN_COUNT] { PenColor.Red, PenColor.Green, PenColor.Blue },
            new PenColor[PEN_COUNT] { PenColor.Blue, PenColor.Red, PenColor.Green },
            new PenColor[PEN_COUNT] { PenColor.Green, PenColor.Blue, PenColor.Red },
        };
        readonly double[] PPMS = new double[PPM_LEVEL_COUNT] {
            1, 1.5, 2, 3, 4, 6, 8, 11, 14, 17, 22, 27, 33, 40, 48, 59
        };
        readonly PenColor ODOR_PEN_COLOR = PenColor.Red;
        readonly Button[] PENS;
        readonly Rectangle[] COLORBOXES;
        readonly Button[] CHOICES;

        // Members

        MFC _mfc = MFC.Instance;

        int _stepID = -1;
        int _currentPenID = -1;
        int _currentPPMLevel = 0;

        PPMChangeDirection _direction = PPMChangeDirection.Increasing;
        int _recognitionsInRowCount = 0;
        List<double> _turningPointPPMs = new List<double>();

        Button CurrentPen => (0 <= _currentPenID && _currentPenID < PENS.Length) ? PENS[_currentPenID] : null;
        PenColor CurrentColor => CurrentPen == null ? PenColor.None : (PenColor)CurrentPen.Tag;

        // Public methods

        public ThreePens()
        {
            InitializeComponent();

            PENS = new Button[PEN_COUNT] { btnPen1, btnPen2, btnPen3 };
            COLORBOXES = new Rectangle[PEN_COUNT] { rctPen1, rctPen2, rctPen3 };
            CHOICES = new Button[PEN_COUNT] { btnChoice1, btnChoice2, btnChoice3 };
        }

        public void Init()
        {
            _currentPenID = -1;

            var order = ORDER_MATRIX[++_stepID % ORDER_MATRIX.Length];
            for (int i = 0; i < PENS.Length; i++)
            {
                PENS[i].Tag = (int)order[i];
                COLORBOXES[i].Tag = (int)order[i];
                CHOICES[i].Tag = (int)order[i];
            }

            ActivateNextPen();
            UpdateDisplay();
        }


        // Internal

        private bool ActivateNextPen()
        {
            if (CurrentColor == ODOR_PEN_COLOR)                     // previous pen was with the odor - switch the mixer back to the fresh air
            {
                _mfc.OdorDirection = MFC.OdorFlow.ToWaste;
                _mfc.OdorSpeed = MFC.ODOR_MIN_SPEED;
            }

            if (++_currentPenID == PENS.Length)
            {
                _currentPenID = -1;
                return false;
            }

            lblInstruction.Text = INSTRUCTION_WAIT;

            var delay1 = _mfc.EstimateFlowDuration(MFC.FlowEndPoint.Mixer, MFC.ODOR_MAX_SPEED);
            var delay2 = 0.5;                                       // from mixer to user

            if (CurrentColor == ODOR_PEN_COLOR)                     // When the pen has the odor, then
            {
                _mfc.OdorSpeed = MFC.ODOR_MAX_SPEED;                // 1. turn the odor sped to the maximum to fill the odor tube quickly with the odor

                Utils.DispatchOnce.Do(delay1, () =>
                {
                    _mfc.OdorSpeed = _mfc.PPM2Speed(PPMS[_currentPPMLevel]);   // 2. after is it filled, turn the speed to the current one
                    Utils.DispatchOnce.Do(delay2, () =>             // 3. wait shortly for the odor to get the proper balance in the mixer chain
                    {
                        _mfc.OdorDirection = MFC.OdorFlow.ToUser;   // 4. finally switch the mixer output to deliver the odor to user
                        CurrentPen.IsEnabled = true;                //    and enable the button 
                        lblInstruction.Text = INSTRUCTION_CLICK;
                    });
                });
            }
            else                                                    // else just enable the button
            {
                Utils.DispatchOnce.Do(delay1 + delay2, () => CurrentPen.IsEnabled = true);
            }

            return true;
        }

        private void EnableChoiceButtons(bool enable)
        {
            for (int i = 0; i < CHOICES.Length; i++)
            {
                CHOICES[i].IsEnabled = enable;
            }
        }

        private Color GetPenColor(PenColor color) => color switch
        {
            PenColor.Red => Colors.Red,
            PenColor.Green => Colors.Green,
            PenColor.Blue => Colors.Blue,
            _ => throw new NotImplementedException("Unrecognized pen color"),
        };

        private void ColorizePens(bool colorize)
        {
            foreach (var box in COLORBOXES)
            {
                box.Fill = colorize ? new SolidColorBrush(GetPenColor((PenColor)box.Tag)) : Brushes.Transparent;
            }
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
            else if (++_recognitionsInRowCount == RECOGNITION_COUNT)               // decrease ppm only if recognized correctly wice in a row
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

        private void UpdateDisplay()
        {
            lblDirection.Content = _direction;
            lblPPMLevel.Content = _currentPPMLevel + 1;
            lblRecognitionsInRow.Content = _recognitionsInRowCount;
            lblStep.Content = _stepID;
            lblTurningCount.Content = _turningPointPPMs.Count;
        }


        // UI events

        private void OnPen_Click(object sender, RoutedEventArgs e)
        {
            var pen = sender as Button;
            pen.IsEnabled = false;

            if (!ActivateNextPen())
            {
                EnableChoiceButtons(true);
            }
        }

        private void OnChoice_Click(object sender, RoutedEventArgs e)
        {
            EnableChoiceButtons(false);

            var choice = sender as Button;
            var isCorrectChoice = (PenColor)choice.Tag == ODOR_PEN_COLOR;
            var canContinue = AdjustPPM(isCorrectChoice);

            ColorizePens(true);
            UpdateDisplay();

            Utils.DispatchOnce.Do(3, () =>
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
