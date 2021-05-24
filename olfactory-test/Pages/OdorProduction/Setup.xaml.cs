using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Olfactory.Pages.OdorProduction
{
    public partial class Setup : Page, IPage<Tests.OdorProduction.Settings>
    {
        public event EventHandler<Tests.OdorProduction.Settings> Next = delegate { };

        public Setup()
        {
            InitializeComponent();
        }


        // Internal

        // NOTE: we store instructions in tags, they are used to show in messages

        private TextBox CheckInput()
        {
            double dVal;
            int iVal;

            if (!double.TryParse(txbFreshAir.Text, out dVal) || dVal <= 0 || dVal > 10)
            {
                return txbFreshAir;
            }

            var odorQuantities = txbOdorQuantities.Text.Split(',');
            if (odorQuantities.Length == 0 || odorQuantities.Any(value => !double.TryParse(value, out double dVal) || dVal <= 0 || dVal > 200))
            {
                return txbOdorQuantities;
            }

            if (!int.TryParse(txbInitialPause.Text, out iVal) || iVal < 0 || iVal > 10000)
            {
                return txbInitialPause;
            }

            if (!int.TryParse(txbOdorFlowDuration.Text, out iVal) || iVal < 0 || iVal > 10000)
            {
                return txbOdorFlowDuration;
            }

            if (!int.TryParse(txbFinalPause.Text, out iVal) || iVal < 0 || iVal > 10000)
            {
                return txbFinalPause;
            }

            return null;
        }


        // UI events

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var improperInput = CheckInput();
            if (improperInput != null)
            {
                MessageBox.Show($"The value '{improperInput.Text}' is not valid: this must be {improperInput.ToolTip}. Please correct and try again", Title, MessageBoxButton.OK, MessageBoxImage.Error);
                improperInput.Focus();
            }
            else
            {
                var settings = new Tests.OdorProduction.Settings()
                {
                    FreshAir = double.Parse(txbFreshAir.Text),
                    OdorQuantities = txbOdorQuantities.Text.Split(',').Select(value => double.Parse(value)).ToArray(),
                    InitialPause = int.Parse(txbInitialPause.Text),
                    OdorFlowDuration = int.Parse(txbOdorFlowDuration.Text),
                    FinalPause = int.Parse(txbFinalPause.Text),
                };
                Next(this, settings);
            }
        }
    }
}
