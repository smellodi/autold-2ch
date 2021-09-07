using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Olfactory.Utils;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Setup : Page, IPage<Tests.ThresholdTest.Settings>
    {
        public event EventHandler<Tests.ThresholdTest.Settings> Next = delegate { };

        public Setup()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            txbFreshAir.Text = _settings.FreshAir.ToString("F1");
            txbPPMs.Text = string.Join(LIST_DELIM, _settings.PPMs);
            txbOdorPreparationDuration.Text = _settings.OdorPreparationDuration.ToString();
            txbPenSniffingDuration.Text = _settings.PenSniffingDuration.ToString();
            txbTurningPoints.Text = _settings.TurningPoints.ToString();
            txbTurningPointsToCount.Text = _settings.TurningPointsToCount.ToString();
            txbRecognitionsInRow.Text = _settings.RecognitionsInRow.ToString();
            txbFamiliarizationDuration.Text = _settings.FamiliarizationDuration.ToString();
            txbPIDSamplingInterval.Text = _settings.PIDReadingInterval.ToString();
            chkFeedbackLoopToReachLevel.IsChecked = _settings.UseFeedbackLoopToReachLevel;
            chkFeedbackLoopToKeepLevel.IsChecked = _settings.UseFeedbackLoopToKeepLevel;
            cmbProcedureFlow.ItemsSource = PROCEDURE_FLOW_TOOLTIPS
                .Select(item => new ComboBoxItem()
                {
                    Content = L10n.T(item.Key.ToString()),
                    ToolTip = item.Value,
                    IsEnabled = item.Key != Tests.ThresholdTest.Procedure.PenPresentationStart.Automatic,
                });
            cmbProcedureFlow.SelectedIndex = (int)_settings.FlowStart;
        }


        // Internal

        const char LIST_DELIM = ' ';

        Dictionary<Tests.ThresholdTest.Procedure.PenPresentationStart, string> PROCEDURE_FLOW_TOOLTIPS = new Dictionary<Tests.ThresholdTest.Procedure.PenPresentationStart, string>()
        {
            { Tests.ThresholdTest.Procedure.PenPresentationStart.Immediate, L10n.T("OdorStartsImmediately") },
            { Tests.ThresholdTest.Procedure.PenPresentationStart.Manual, L10n.T("OdorStartsAfterKeyPress") },
            { Tests.ThresholdTest.Procedure.PenPresentationStart.Automatic, L10n.T("OdorStartsAfterInhale") },
        };

        Tests.ThresholdTest.Settings _settings = new Tests.ThresholdTest.Settings();

        private Utils.Validation CheckInput()
        {
            int.TryParse(txbTurningPoints.Text, out int maxTurningPoints);

            var validations = new Utils.Validation[]
            {
                new Utils.Validation(txbFreshAir, 1, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbPPMs, 0.5, 250, Utils.Validation.ValueFormat.Float, LIST_DELIM),
                new Utils.Validation(txbFamiliarizationDuration, 1, 5, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbOdorPreparationDuration, 10, 300, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbPenSniffingDuration, 0.2, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbTurningPoints, 2, 15, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbTurningPointsToCount, 2, maxTurningPoints, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbRecognitionsInRow, 1, 5, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbPIDSamplingInterval, 100, 5000, Utils.Validation.ValueFormat.Integer),
            };

            foreach (var v in validations)
            {
                if (!v.IsValid)
                {
                    return v;
                }
            }

            return null;
        }


        // UI events

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var validation = CheckInput();
            if (validation != null)
            {
                var msg = L10n.T("CorrectAndTryAgain");
                MessageBox.Show(
                    $"{validation}.\n{msg}",
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                validation.Source.Focus();
                validation.Source.SelectAll();
            }
            else
            {
                _settings.FreshAir = double.Parse(txbFreshAir.Text);
                _settings.PPMs = txbPPMs.Text
                    .Split(LIST_DELIM)
                    .Select(val => double.Parse(val))
                    .ToArray(); 
                _settings.OdorPreparationDuration = int.Parse(txbOdorPreparationDuration.Text);
                _settings.PenSniffingDuration = double.Parse(txbPenSniffingDuration.Text);
                _settings.TurningPoints = int.Parse(txbTurningPoints.Text);
                _settings.TurningPointsToCount = int.Parse(txbTurningPointsToCount.Text);
                _settings.RecognitionsInRow = int.Parse(txbRecognitionsInRow.Text);
                _settings.FamiliarizationDuration = double.Parse(txbFamiliarizationDuration.Text);
                _settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);
                _settings.UseFeedbackLoopToReachLevel = chkFeedbackLoopToReachLevel.IsChecked ?? false;
                _settings.UseFeedbackLoopToKeepLevel = chkFeedbackLoopToKeepLevel.IsChecked ?? false;
                _settings.FlowStart = (Tests.ThresholdTest.Procedure.PenPresentationStart)cmbProcedureFlow.SelectedIndex;

                _settings.Save();

                Next(this, _settings);
            }
        }
    }
}
