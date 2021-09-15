using System;
using System.Windows.Controls;
using System.Windows.Input;
using ThreePensProc = Olfactory.Tests.ThresholdTest.ThreePens;
using FlowStart = Olfactory.Tests.ThresholdTest.Settings.FlowStartTrigger;

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

            PENS = new Controls.Pen[ThreePensProc.PEN_COUNT] { pen1, pen2, pen3 };

            _procedure.OdorPreparation += (s, e) => Dispatcher.Invoke(() => wtiInstruction.Progress = e);

            _procedure.PenActivated += (s, e) => Dispatcher.Invoke(() => {
                wtiInstruction.Reset();
                ActivatePen(e.ID, e.FlowStart);
            });

            _procedure.OdorFlowStarted += (s, e) => Dispatcher.Invoke(() => {
                if (_procedure.FlowStarts != FlowStart.Immediate)
                {
                    wtiInstruction.Text = "";   // clear the instruction that tells to press the SPACE key / make inhale
                }
            });

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

            wtiInstruction.Text = INSTRUCTION_WAIT_FOR_THE_TRIAL_TO_START;
        }

        public void Interrupt()
        {
            _procedure.Stop();
        }

        public void ConsumeKeyDown(Key e)
        {
            if ((e == Key.Space && _procedure.FlowStarts == FlowStart.Manual) ||
                (e == Key.Enter && _procedure.FlowStarts == FlowStart.Automatic))
            {
                _procedure.EnablePenOdor();
            }
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

        ThreePensProc _procedure = new();
        int _currentPenID = -1;

        Controls.Pen CurrentPen => (0 <= _currentPenID && _currentPenID < PENS.Length) ? PENS[_currentPenID] : null;


        readonly string INSTRUCTION_SNIFF_THE_PEN_FIXED = Utils.L10n.T("ThTestInstrSniff");
        readonly string INSTRUCTION_SNIFF_THE_PEN_MANUAL = Utils.L10n.T("ThTestInstrPressKey");
        readonly string INSTRUCTION_SNIFF_THE_PEN_AUTO = Utils.L10n.T("ThTestInstrInhale");
        readonly string INSTRUCTION_WAIT_FOR_THE_TRIAL_TO_START = Utils.L10n.T("ThTestInstrWait");
        readonly string INSTRUCTION_CHOOSE_THE_PEN = Utils.L10n.T("ThTestInstrSelectPen");
        readonly string INSTRUCTION_DONE = Utils.L10n.T("ThTestInstrDone");

        readonly Controls.Pen[] PENS;

        private void ActivatePen(int penID, FlowStart flowStart)
        {
            if (CurrentPen != null)
            {
                CurrentPen.IsActive = false;
            }
            _currentPenID = penID;

            wtiInstruction.Text = flowStart switch
            {
                FlowStart.Immediate => INSTRUCTION_SNIFF_THE_PEN_FIXED,
                FlowStart.Manual => INSTRUCTION_SNIFF_THE_PEN_MANUAL,
                FlowStart.Automatic => INSTRUCTION_SNIFF_THE_PEN_AUTO,
                _ => ""
            };

            CurrentPen.IsActive = true;
        }

        // UI actions

        private void EnableChoiceButtons(bool enable)
        {
            wtiInstruction.Text = enable ? INSTRUCTION_CHOOSE_THE_PEN : INSTRUCTION_DONE;

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
