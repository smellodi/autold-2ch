using System;
using System.Windows.Controls;
using Procedure = Olfactory.Tests.ThresholdTest.Procedure;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class ThreePens : Page, IPage<double>
    {
        public event EventHandler<double> Next = delegate { };

        public Tests.ITestEmulator Emulator => _procedure;

        public ThreePens()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            PENS = new Controls.Pen[Procedure.PEN_COUNT] { pen1, pen2, pen3 };

            _procedure.PenActivated += (s, e) => Dispatcher.Invoke(() => ActivatePen(e));

            _procedure.WaitingForPenSelection += (s, e) => Dispatcher.Invoke(() => {
                if (CurrentPen != null)
                {
                    CurrentPen.IsActive = false;
                }
                EnableChoiceButtons(true);
            });

            _procedure.Next += (s, e) => Dispatcher.Invoke(() => {
                ColorizePens(false);
                Utils.DispatchOnceUI.Do(0.1, () => Init());
            });

            _procedure.Finished += (s, e) => Dispatcher.Invoke(() => {
                ColorizePens(false);
                Next(this, e);
            });
        }

        public void Init(Tests.ThresholdTest.Settings settings = null)
        {
            _currentPenID = -1;

            var pens = _procedure.Start(settings);
            for (int i = 0; i < PENS.Length; i++)
            {
                PENS[i].PenInstance = pens[i];
            }

            UpdateDisplay(Update.All);

            lblInstruction.Text = INSTRUCTION_WAIT_FOR_THE_TRIAL_TO_START;
        }

        public void Interrupt()
        {
            _procedure.Interrupt();
        }


        // Internal

        [Flags]
        enum Update
        {
            Step = 1,
            PPM = 2,
            Recognitions = 4,
            Turnings = 8,
            All = Step | PPM | Recognitions | Turnings
        }

        Procedure _procedure = new Procedure();
        int _currentPenID = -1;

        Controls.Pen CurrentPen => (0 <= _currentPenID && _currentPenID < PENS.Length) ? PENS[_currentPenID] : null;


        string INSTRUCTION_SNIFF_THE_PEN(int id) => $"Sniff the pen #{id + 1}";
        const string INSTRUCTION_WAIT_FOR_THE_TRIAL_TO_START = "Please wait until the odorant in ready.";
        const string INSTRUCTION_CHOOSE_THE_PEN = "Please select the pen with the odorant.";
        const string INSTRUCTION_DONE = "Thanks, your choice has been recorded.";

        readonly Controls.Pen[] PENS;

        private void ActivatePen(int penID)
        {
            if (CurrentPen != null)
            {
                CurrentPen.IsActive = false;
            }
            _currentPenID = penID;

            lblInstruction.Text = INSTRUCTION_SNIFF_THE_PEN(_currentPenID);

            CurrentPen.IsActive = true;
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
                lblDirection.Content = _procedure.Direction;
                lblPPMLevel.Content = _procedure.PPMLevel;
            }
            if (update.HasFlag(Update.Recognitions))
            {
                lblRecognitionsInRow.Content = _procedure.RecognitionsInRow;
            }
            if (update.HasFlag(Update.Step))
            {
                lblStep.Content = _procedure.Step;
            }
            if (update.HasFlag(Update.Turnings))
            {
                lblTurningCount.Content = _procedure.TurningPointCount;
            }
        }

        private void ColorizePens(bool colorize)
        {
            foreach (var pen in PENS)
            {
                pen.IsColorVisible = colorize;
            }
        }


        // UI events

        private void OnPen_Selected(object sender, EventArgs e)
        {
            EnableChoiceButtons(false);

            var penControl = sender as Controls.Pen;
            _procedure.Select(penControl.PenInstance);

            ColorizePens(true);

            UpdateDisplay(Update.Recognitions | Update.Turnings);
        }
    }
}
