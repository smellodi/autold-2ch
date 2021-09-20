using System;
using System.Windows.Controls;
using System.Windows.Input;
using PenProc = Olfactory.Tests.ThresholdTest.ProcedurePens;
using FlowStart = Olfactory.Tests.ThresholdTest.Settings.FlowStartTrigger;
using BreathingStage = Olfactory.Tests.ThresholdTest.BreathingDetector.Stage;
using System.Collections.Generic;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class PenPresentation : Page, IPage<double>
    {
        public event EventHandler<double> Next = delegate { };

        public Tests.ITestEmulator Emulator => _procedure;

        public PenPresentation()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            _procedure.OdorPreparation += (s, e) => Dispatcher.Invoke(() => wtiInstruction.Progress = e);

            _procedure.PenActivated += (s, e) => Dispatcher.Invoke(() => {
                wtiInstruction.Reset();
                ActivatePen(e.ID, e.FlowStart);
            });

            _procedure.OdorFlowStarted += (s, e) => Dispatcher.Invoke(() => {
                if (_procedure.FlowStart != FlowStart.Immediate)
                {
                    wtiInstruction.Text = "";   // clear the instruction that tells to press the SPACE key / make inhale
                }
            });

            _procedure.WaitingForAnswer += (s, e) => Dispatcher.Invoke(() => {
                if (CurrentPen != null)
                {
                    CurrentPen.IsActive = false;
                }

                wtiInstruction.Text = e == PenProc.AnswerType.HasOdor ? INSTRUCTION_CHOOSE_THE_PEN : INSTRUCTION_CHOOSE_PEN_ODOR;

                foreach (var pen in _pens)
                {
                    pen.IsSelectable = true;
                }
            });

            _procedure.Next += (s, e) => Dispatcher.Invoke(() => {
                ColorizePens(false);
                Utils.DispatchOnceUI.Do(0.1, () => Init());
            });

            _procedure.Finished += (s, e) => Dispatcher.Invoke(() => {
                ColorizePens(false);
                Next(this, e);
            });

            _procedure.BreathingDetector.StageChanged += (s, e) => Dispatcher.Invoke(() => {
                elpBreathingStage.Fill = e switch
                {
                    BreathingStage.Inhale => Tests.ThresholdTest.BreathingDetector.InhaleBrush,
                    BreathingStage.Exhale => Tests.ThresholdTest.BreathingDetector.ExhaleBrush,
                    _ => System.Windows.Media.Brushes.White
                };
            });
        }

        public void Init(Tests.ThresholdTest.Settings settings = null)
        {
            var pens = _procedure.Start(settings);

            grdPens.MaxWidth = MAX_PEN_AREA_WIDTH * _procedure.PenCount;

            while (grdPens.ColumnDefinitions.Count < _procedure.PenCount)
            {
                var pen = new Controls.Pen
                {
                    ID = (grdPens.ColumnDefinitions.Count + 1).ToString(),
                    CanChoose = _procedure.CanChoose
                };
                pen.Selected += OnPen_Selected;

                Grid.SetRow(pen, 1);
                Grid.SetColumn(pen, grdPens.ColumnDefinitions.Count);

                _pens.Add(pen);

                grdPens.ColumnDefinitions.Add(new ColumnDefinition());
                grdPens.Children.Add(pen);
            }

            for (int i = 0; i < _pens.Count; i++)
            {
                _pens[i].PenInstance = pens[i];
            }

            _currentPenID = -1;

            UpdateDisplay(Update.All);

            elpBreathingStage.Visibility = _procedure.FlowStart == FlowStart.Automatic ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

            wtiInstruction.Text = INSTRUCTION_WAIT_FOR_THE_TRIAL_TO_START;
        }

        public void Interrupt()
        {
            _procedure.Stop();
        }

        public void ConsumeKeyDown(Key e)
        {
            if ((e == Key.Space && _procedure.FlowStart == FlowStart.Manual) ||
                (e == Key.Enter && _procedure.FlowStart == FlowStart.Automatic))
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


        const int MAX_PEN_AREA_WIDTH = 320;

        readonly string INSTRUCTION_SNIFF_THE_PEN_FIXED = Utils.L10n.T("ThTestInstrSniff");
        readonly string INSTRUCTION_SNIFF_THE_PEN_MANUAL = Utils.L10n.T("ThTestInstrPressKey");
        readonly string INSTRUCTION_SNIFF_THE_PEN_AUTO = Utils.L10n.T("ThTestInstrInhale");
        readonly string INSTRUCTION_WAIT_FOR_THE_TRIAL_TO_START = Utils.L10n.T("ThTestInstrWait");
        readonly string INSTRUCTION_CHOOSE_THE_PEN = Utils.L10n.T("ThTestInstrSelectPen");
        readonly string INSTRUCTION_CHOOSE_PEN_ODOR = Utils.L10n.T("ThTestInstrChooseOdor");
        readonly string INSTRUCTION_DONE = Utils.L10n.T("ThTestInstrDone");

        readonly List<Controls.Pen> _pens = new();
        readonly PenProc _procedure = new();

        int _currentPenID = -1;

        Controls.Pen CurrentPen => (0 <= _currentPenID && _currentPenID < _pens.Count) ? _pens[_currentPenID] : null;


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

        private void UpdateDisplay(Update update)
        {
            if (update.HasFlag(Update.PPM))
            {
                lblDirection.Content = Utils.L10n.T(_procedure.Direction.ToString());
                lblPPM.Content = _procedure.PPM;
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
            foreach (var pen in _pens)
            {
                pen.IsColorVisible = colorize;
            }
        }


        // UI events

        private void OnPen_Selected(object sender, bool answer)
        {
            wtiInstruction.Text = INSTRUCTION_DONE;

            foreach (var pen in _pens)
            {
                pen.IsSelectable = false;
            }

            var penControl = sender as Controls.Pen;
            _procedure.Select(answer ? penControl.PenInstance : null);

            ColorizePens(true);

            UpdateDisplay(Update.Recognitions | Update.Turnings);
        }
    }
}
