using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
            txbPIDSamplingInterval.Text = _settings.PIDReadingInterval.ToString();
        }


        // Internal

        const char LIST_DELIM = ' ';

        const NumberStyles INTEGER = NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        const NumberStyles FLOAT = NumberStyles.Float;

        Tests.ThresholdTest.Settings _settings = new Tests.ThresholdTest.Settings();

        private TextBox CheckInput()
        {
            bool IsValidValue(string value)
            {
                return double.TryParse(value, FLOAT, null, out double dVal) && dVal > 0 && dVal <= Comm.MFC.ODOR_MAX_SPEED;
            }

            double dVal;
            int iVal;

            if (!double.TryParse(txbFreshAir.Text, FLOAT, null, out dVal) || dVal <= 0 || dVal > 10)
            {
                return txbFreshAir;
            }

            var odorQuantities = txbPPMs.Text.Split(LIST_DELIM);
            if (odorQuantities.Length == 0 || odorQuantities.Any(quant => !IsValidValue(quant)))
            {
                return txbPPMs;
            }

            if (!int.TryParse(txbOdorPreparationDuration.Text, INTEGER, null, out iVal) || iVal < 10 || iVal > 300)
            {
                return txbOdorPreparationDuration;
            }

            if (!int.TryParse(txbPenSniffingDuration.Text, INTEGER, null, out iVal) || iVal < 3 || iVal > 30)
            {
                return txbPenSniffingDuration;
            }

            if (!int.TryParse(txbTurningPoints.Text, INTEGER, null, out iVal) || iVal < 2 || iVal > 15)
            {
                return txbTurningPoints;
            }

            var turningPoints = iVal;
            if (!int.TryParse(txbTurningPointsToCount.Text, INTEGER, null, out iVal) || iVal < 2 || iVal > turningPoints)
            {
                return txbTurningPointsToCount;
            }

            if (!int.TryParse(txbRecognitionsInRow.Text, INTEGER, null, out iVal) || iVal < 1 || iVal > 5)
            {
                return txbRecognitionsInRow;
            }

            if (!int.TryParse(txbPIDSamplingInterval.Text, INTEGER, null, out iVal) || iVal < 100 || iVal > 5000)
            {
                return txbPIDSamplingInterval;
            }

            return null;
        }


        // UI events

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var improperInput = CheckInput();
            if (improperInput != null)
            {
                MessageBox.Show(
                    $"The value '{improperInput.Text}' is not valid, it must be {improperInput.ToolTip}. Please correct and try again",
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                improperInput.Focus();
                improperInput.SelectAll();
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
                _settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);

                _settings.Save();

                Next(this, _settings);
            }
        }
    }
}
