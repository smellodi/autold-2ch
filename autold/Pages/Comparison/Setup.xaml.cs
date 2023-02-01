using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Olfactory.Tests.Comparison;

namespace Olfactory.Pages.Comparison
{
    public partial class Setup : Page, IPage<Settings>, Tests.ITestEmulator
    {
        public event EventHandler<Settings> Next;

        public Setup()
        {
            InitializeComponent();

            Storage.Instance
                .BindScaleToZoomLevel(sctScale)
                .BindVisibilityToDebug(lblDebug);

            cmbGas1.ItemsSource = Enum.GetValues(typeof(Gas));
            cmbGas2.ItemsSource = Enum.GetValues(typeof(Gas));

            txbFreshAirFlow.Text = _settings.FreshAirFlow.ToString("F1");
            txbOdorFlow.Text = _settings.OdorFlow.ToString("F1");
            cmbGas1.SelectedItem = _settings.Gas1;
            cmbGas2.SelectedItem = _settings.Gas2;
            txbInitialPause.Text = _settings.InitialPause.ToString();
            txbOdorFlowDuration.Text = _settings.OdorFlowDuration.ToString();
            txbPairsOfMixtures.Text = _settings.SerializeMixtures();
            //txbPIDSamplingInterval.Text = _settings.PIDReadingInterval.ToString();
        }

        public void EmulationInit() { }

        public void EmulationFinilize() { }


        // Internal

        readonly Settings _settings = new();

        private Utils.Validation CheckInput()
        {
            var pairsOfMixtures = Settings.ParsePairsOfMixtures(txbPairsOfMixtures.Text.Replace("\r\n", "\n"), out string error);
            if (pairsOfMixtures == null)
            {
                return new Utils.Validation(txbPairsOfMixtures, error);
            }

            var validations = new List<Utils.Validation>
            {
                new Utils.Validation(txbFreshAirFlow, 1, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbOdorFlow, 1, 200, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbInitialPause, 0, 10000, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbOdorFlowDuration, 0.1, Comm.MFC.MAX_SHORT_PULSE_DURATION / 1000, Utils.Validation.ValueFormat.Float),
                //new Utils.Validation(txbPIDSamplingInterval, 100, 5000, Utils.Validation.ValueFormat.Integer),
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

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            var validation = CheckInput();
            if (validation != null)
            {
                var msg = Utils.L10n.T("CorrectAndTryAgain");
                Utils.MsgBox.Error(App.Name, $"{validation}.\n{msg}");
                validation.Source.Focus();
                validation.Source.SelectAll();
            }
            else
            {
                _settings.FreshAirFlow = double.Parse(txbFreshAirFlow.Text);
                _settings.OdorFlow = double.Parse(txbOdorFlow.Text);
                _settings.Gas1 = (Gas)cmbGas1.SelectedItem;
                _settings.Gas2 = (Gas)cmbGas2.SelectedItem;
                _settings.InitialPause = int.Parse(txbInitialPause.Text);
                _settings.OdorFlowDuration = double.Parse(txbOdorFlowDuration.Text);
                _settings.PairsOfMixtures = Settings.ParsePairsOfMixtures(txbPairsOfMixtures.Text.Replace("\r\n", "\n"), out string _);
                //_settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);

                _settings.Save();

                Next?.Invoke(this, _settings);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, null);
        }
    }
}
