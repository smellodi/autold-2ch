using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;

namespace Olfactory.Pages.OdorProduction
{
    public partial class Setup : Page, IPage<Tests.OdorProduction.Settings>, Tests.ITestEmulator
    {
        public event EventHandler<Tests.OdorProduction.Settings> Next = delegate { };

        public Setup()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            txbFreshAir.Text = _settings.FreshAir.ToString("F1");
            txbOdorQuantities.Text = string.Join(LIST_DELIM + " ", _settings.OdorQuantities);
            txbInitialPause.Text = _settings.InitialPause.ToString();
            txbOdorFlowDuration.Text = _settings.OdorFlowDuration.ToString();
            txbFinalPause.Text = _settings.FinalPause.ToString();
            txbPIDSamplingInterval.Text = _settings.PIDReadingInterval.ToString();
            rdbValve2ToWaste.IsChecked = !_settings.Valve2ToUser;
            rdbValve2ToUser.IsChecked = _settings.Valve2ToUser;
            chkFeedbackLoop.IsChecked = _settings.UseFeedbackLoop;
        }

        public void EmulationInit() { }

        public void EmulationFinilize() { }


        // Internal

        const char LIST_DELIM = ',';
        readonly char[] EXPR_OPS = new char[] { 'x', '*' };

        const NumberStyles INTEGER = NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        const NumberStyles FLOAT = NumberStyles.Float;

        Tests.OdorProduction.Settings _settings = new Tests.OdorProduction.Settings();

        private TextBox CheckInput()
        {
            double dVal;
            int iVal;

            if (!double.TryParse(txbFreshAir.Text, FLOAT, null, out dVal) || dVal <= 0 || dVal > 10)
            {
                return txbFreshAir;
            }

            var odorQuantities = txbOdorQuantities.Text.Split(LIST_DELIM);
            if (odorQuantities.Length == 0 || odorQuantities.Any(quant => !IsValidValueOrExpression(quant)))
            {
                return txbOdorQuantities;
            }

            if (!int.TryParse(txbInitialPause.Text, INTEGER, null, out iVal) || iVal < 0 || iVal > 10000)
            {
                return txbInitialPause;
            }

            if (!int.TryParse(txbOdorFlowDuration.Text, INTEGER, null, out iVal) || iVal < 1 || iVal > 10000)
            {
                return txbOdorFlowDuration;
            }

            if (!int.TryParse(txbFinalPause.Text, INTEGER, null, out iVal) || iVal < 0 || iVal > 10000)
            {
                return txbFinalPause;
            }

            if (!int.TryParse(txbPIDSamplingInterval.Text, INTEGER, null, out iVal) || iVal < 100 || iVal > 5000)
            {
                return txbPIDSamplingInterval;
            }

            return null;
        }

        private bool IsValidValueOrExpression(string value)
        {
            var exprValues = value.Split(EXPR_OPS);
            if (exprValues.Length > 1)
            {
                return exprValues.All(exprValue => IsValidValueOrExpression(exprValue));
            }
            else
            {
                return double.TryParse(value, FLOAT, null, out double dVal) && dVal > 0 && dVal <= Comm.MFC.ODOR_MAX_SPEED;
            }
        }

        private double[] AsValues(string[] expressions)
        {
            return expressions.SelectMany<string, double>(expression =>
            {
                var exprValues = expression.Split(EXPR_OPS);
                if (exprValues.Length == 1)
                {
                    return new double[] { double.Parse(expression) };
                }
                else
                {
                    var value = double.Parse(exprValues[0]);
                    var count = double.Parse(exprValues[1]);
                    List<double> values = new List<double>();
                    for (int i = 0; i < count; i++)
                    {
                        values.Add(value);
                    }
                    return values;
                };
            }).ToArray();
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
                _settings.OdorQuantities = AsValues(txbOdorQuantities.Text.Split(LIST_DELIM));
                _settings.InitialPause = int.Parse(txbInitialPause.Text);
                _settings.OdorFlowDuration = int.Parse(txbOdorFlowDuration.Text);
                _settings.FinalPause = int.Parse(txbFinalPause.Text);
                _settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);
                _settings.Valve2ToUser = rdbValve2ToUser.IsChecked ?? false;
                _settings.UseFeedbackLoop = chkFeedbackLoop.IsChecked ?? false;

                _settings.Save();

                Next(this, _settings);
            }
        }
    }
}
