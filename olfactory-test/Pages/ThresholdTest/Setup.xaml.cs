using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Olfactory.Pages.ThresholdTest
{
    public class ProcedureFlowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.ToString() == Tests.ThresholdTest.Procedure.FlowType.FixedTime.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) { return ""; }
    }


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
            cmbProcedureFlow.ItemsSource = Enum.GetNames(typeof(Tests.ThresholdTest.Procedure.FlowType));
            cmbProcedureFlow.SelectedIndex = (int)_settings.FlowType;
        }


        // Internal

        const char LIST_DELIM = ' ';

        Tests.ThresholdTest.Settings _settings = new Tests.ThresholdTest.Settings();

        private Utils.Validation CheckInput()
        {
            int.TryParse(txbTurningPoints.Text, out int maxTurningPoints);

            var validations = new Utils.Validation[]
            {
                new Utils.Validation(txbFreshAir, 1, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbPPMs, 1, 250, Utils.Validation.ValueFormat.Float, LIST_DELIM),
                new Utils.Validation(txbOdorPreparationDuration, 10, 300, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbPenSniffingDuration, 1, 10, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbTurningPoints, 2, 15, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbTurningPointsToCount, 2, maxTurningPoints, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbRecognitionsInRow, 1, 5, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbFamiliarizationDuration, 1, 5, Utils.Validation.ValueFormat.Integer),
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
                MessageBox.Show(
                    $"{validation}.\nPlease correct and try again.",
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
                _settings.PenSniffingDuration = int.Parse(txbPenSniffingDuration.Text);
                _settings.TurningPoints = int.Parse(txbTurningPoints.Text);
                _settings.TurningPointsToCount = int.Parse(txbTurningPointsToCount.Text);
                _settings.RecognitionsInRow = int.Parse(txbRecognitionsInRow.Text);
                _settings.FamiliarizationDuration = double.Parse(txbFamiliarizationDuration.Text);
                _settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);
                _settings.UseFeedbackLoopToReachLevel = chkFeedbackLoopToReachLevel.IsChecked ?? false;
                _settings.UseFeedbackLoopToKeepLevel = chkFeedbackLoopToKeepLevel.IsChecked ?? false;
                _settings.FlowType = (Tests.ThresholdTest.Procedure.FlowType)cmbProcedureFlow.SelectedIndex;

                _settings.Save();

                Next(this, _settings);
            }
        }
    }
}
