using System;
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

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            _valvesControlled = _settings.ValvesControlled;

            txbFreshAir.Text = _settings.FreshAir.ToString("F1");
            txbOdorQuantities.Text = _settings.OdorQuantitiesAsString();
            txbInitialPause.Text = _settings.InitialPause.ToString();
            txbOdorFlowDuration.Text = _settings.OdorFlowDuration.ToString();
            txbFinalPause.Text = _settings.FinalPause.ToString();
            txbPIDSamplingInterval.Text = _settings.PIDReadingInterval.ToString();
            rdbValve1.IsChecked = _valvesControlled == Comm.MFC.ValvesOpened.Valve1;
            rdbValve2.IsChecked = _valvesControlled == Comm.MFC.ValvesOpened.Valve2;
            rdbAllValves.IsChecked = _valvesControlled == Comm.MFC.ValvesOpened.All;
            chkUseValveControllerTimer.IsChecked = _settings.UseValveTimer;
        }

        public void EmulationInit() { }

        public void EmulationFinilize() { }


        // Internal

        readonly Settings _settings = new();

        Comm.MFC.ValvesOpened _valvesControlled;

        private Utils.Validation CheckInput()
        {
            var validations = new Utils.Validation[]
            {
                new Utils.Validation(txbFreshAir, 1, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbInitialPause, 0, 10000, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbOdorFlowDuration, 0.1, 10000, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbFinalPause, 0, 10000, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbPIDSamplingInterval, 100, 5000, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbOdorQuantities, 1, 60000, Utils.Validation.ValueFormat.Float, Settings.LIST_DELIM, Settings.EXPR_OPS),
            };

            foreach (var v in validations)
            {
                if (!v.IsValid)
                {
                    return v;
                }
            }

            var longestDuration = 0.001 * Settings.GetOdorQuantitiesLongestDuration(txbOdorQuantities.Text);
            var isOdorFlowDurationLongEnough = new Utils.Validation(txbOdorFlowDuration, longestDuration, 10000, Utils.Validation.ValueFormat.Float);
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
                Utils.MsgBox.Error(Title, $"{validation}.\n{msg}");
                validation.Source.Focus();
                validation.Source.SelectAll();
            }
            else
            {
                _settings.FreshAir = double.Parse(txbFreshAir.Text);
                _settings.OdorQuantities = Settings.ParseOdorQuantinies(txbOdorQuantities.Text);
                _settings.InitialPause = int.Parse(txbInitialPause.Text);
                _settings.OdorFlowDuration = double.Parse(txbOdorFlowDuration.Text);
                _settings.FinalPause = int.Parse(txbFinalPause.Text);
                _settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);
                _settings.ValvesControlled = _valvesControlled;
                _settings.UseValveTimer = chkUseValveControllerTimer.IsChecked ?? false;

                _settings.Save();

                Next?.Invoke(this, _settings);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, null);
        }

        private void Valve1_Checked(object sender, RoutedEventArgs e)
        {
            _valvesControlled = Comm.MFC.ValvesOpened.Valve1;
        }

        private void Valve2_Checked(object sender, RoutedEventArgs e)
        {
            _valvesControlled = Comm.MFC.ValvesOpened.Valve2;
        }

        private void AllValve_Checked(object sender, RoutedEventArgs e)
        {
            _valvesControlled = Comm.MFC.ValvesOpened.All;
        }
    }
}
