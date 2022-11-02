using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Settings = Olfactory.Tests.OdorProduction.Settings;

namespace Olfactory.Pages.OdorProduction
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

            txbFreshAir.Text = _settings.FreshAir.ToString("F1");
            txbPulses.Text = _settings.SerializePulses();
            txbInitialPause.Text = _settings.InitialPause.ToString();
            txbOdorFlowDuration.Text = _settings.OdorFlowDuration.ToString();
            txbFinalPause.Text = _settings.FinalPause.ToString();
            txbPIDSamplingInterval.Text = _settings.PIDReadingInterval.ToString();
            chkUseValveControllerTimer.IsChecked = _settings.UseValveTimer;
            chkManualFlowStop.IsChecked = _settings.ManualFlowStop;
        }

        public void EmulationInit() { }

        public void EmulationFinilize() { }


        // Internal

        readonly Settings _settings = new();

        private Utils.Validation CheckInput()
        {
            var pulses = Settings.ParsePulses(txbPulses.Text.Replace("\r\n", "\n"), out string error);
            if (pulses == null)
            {
                return new Utils.Validation(txbPulses, error);
            }

            double maxPulseDurationSec = (chkUseValveControllerTimer.IsChecked ?? false) ? Comm.MFC.MAX_SHORT_PULSE_DURATION / 1000 : 10000;

            var validations = new List<Utils.Validation>
            {
                new Utils.Validation(txbFreshAir, 1, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbInitialPause, 0, 10000, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbOdorFlowDuration, 0.1, maxPulseDurationSec, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbFinalPause, 0, 10000, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbPIDSamplingInterval, 100, 5000, Utils.Validation.ValueFormat.Integer),
            };

            foreach (var pulse in pulses)
            {
                if (pulse.Channel1 != null)
                {
                    validations.Add(new Utils.Validation(txbPulses, pulse.Channel1.Delay.ToString(), 0, 65000, Utils.Validation.ValueFormat.Integer));
                    validations.Add(new Utils.Validation(txbPulses, pulse.Channel1.GetDuration(0).ToString(), 0, Comm.MFC.MAX_SHORT_PULSE_DURATION, Utils.Validation.ValueFormat.Integer));
                    validations.Add(new Utils.Validation(txbPulses, pulse.Channel1.Flow.ToString(), 0, 250, Utils.Validation.ValueFormat.Float));
                }
                if (pulse.Channel2 != null)
                {
                    validations.Add(new Utils.Validation(txbPulses, pulse.Channel2.Delay.ToString(), 0, 65000, Utils.Validation.ValueFormat.Integer));
                    validations.Add(new Utils.Validation(txbPulses, pulse.Channel2.GetDuration(0).ToString(), 0, Comm.MFC.MAX_SHORT_PULSE_DURATION, Utils.Validation.ValueFormat.Integer));
                    validations.Add(new Utils.Validation(txbPulses, pulse.Channel2.Flow.ToString(), 0, 250, Utils.Validation.ValueFormat.Float));
                }
            }

            foreach (var v in validations)
            {
                if (!v.IsValid)
                {
                    return v;
                }
            }

            double.TryParse(txbOdorFlowDuration.Text, out double odorFlowDurationSec);

            var longestDurationMs = pulses.Max(pulse => pulse.GetDuration((int)(odorFlowDurationSec * 1000)));
            var isOdorFlowDurationLongEnough = new Utils.Validation(txbOdorFlowDuration, 0.001 * longestDurationMs, maxPulseDurationSec, Utils.Validation.ValueFormat.Float);
            if (!isOdorFlowDurationLongEnough.IsValid)
            {
                return isOdorFlowDurationLongEnough;
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
                _settings.FreshAir = double.Parse(txbFreshAir.Text);
                _settings.Pulses = Settings.ParsePulses(txbPulses.Text.Replace("\r\n", "\n"), out string _);
                _settings.InitialPause = int.Parse(txbInitialPause.Text);
                _settings.OdorFlowDuration = double.Parse(txbOdorFlowDuration.Text);
                _settings.FinalPause = int.Parse(txbFinalPause.Text);
                _settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);
                _settings.UseValveTimer = chkUseValveControllerTimer.IsChecked ?? false;
                _settings.ManualFlowStop = chkManualFlowStop.IsChecked ?? false;

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
