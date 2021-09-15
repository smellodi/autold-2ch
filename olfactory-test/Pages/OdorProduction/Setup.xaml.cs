using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
            chkFeedbackLoopToReachLevel.IsChecked = _settings.UseFeedbackLoopToReachLevel;
            chkFeedbackLoopToKeepLevel.IsChecked = _settings.UseFeedbackLoopToKeepLevel;
        }

        public void EmulationInit() { }

        public void EmulationFinilize() { }


        // Internal

        readonly char LIST_DELIM = ',';
        readonly char[] EXPR_OPS = new char[] { 'x', '*' };

        Tests.OdorProduction.Settings _settings = new Tests.OdorProduction.Settings();

        private Utils.Validation CheckInput()
        {
            var validations = new Utils.Validation[]
            {
                new Utils.Validation(txbFreshAir, 1, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbOdorQuantities, 1, 250, Utils.Validation.ValueFormat.Float, LIST_DELIM, EXPR_OPS),
                new Utils.Validation(txbInitialPause, 0, 10000, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbOdorFlowDuration, 0.1, 10000, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbFinalPause, 0, 10000, Utils.Validation.ValueFormat.Integer),
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

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            var validation = CheckInput();
            if (validation != null)
            {
                var msg = Utils.L10n.T("CorrectAndTryAgain");
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
                _settings.OdorQuantities = AsValues(txbOdorQuantities.Text.Split(LIST_DELIM));
                _settings.InitialPause = int.Parse(txbInitialPause.Text);
                _settings.OdorFlowDuration = double.Parse(txbOdorFlowDuration.Text);
                _settings.FinalPause = int.Parse(txbFinalPause.Text);
                _settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);
                _settings.Valve2ToUser = rdbValve2ToUser.IsChecked ?? false;
                _settings.UseFeedbackLoopToReachLevel = chkFeedbackLoopToReachLevel.IsChecked ?? false;
                _settings.UseFeedbackLoopToKeepLevel = chkFeedbackLoopToKeepLevel.IsChecked ?? false;

                _settings.Save();

                Next(this, _settings);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Next(this, null);
        }
    }
}
