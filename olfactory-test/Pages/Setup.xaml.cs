using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Olfactory.Comm;

namespace Olfactory.Pages
{
    public partial class Setup : Page, IPage<Tests.Test>
    {
        public event EventHandler<Tests.Test> Next = delegate { };

        public double Scale { get; private set; } = 1;

        public Setup()
        {
            InitializeComponent();

            Storage.Instance
                .BindScaleToZoomLevel(sctScale)
                .BindVisibilityToDebug(lblDebug);

            UpdatePortList(cmbPIDPort);
            UpdatePortList(cmbMFCPort);

            Application.Current.Exit += (s, e) => Close();

            LoadSettings();

            _usb.Inserted += (s, e) => Dispatcher.Invoke(() =>
                {
                    UpdatePortList(cmbPIDPort);
                    UpdatePortList(cmbMFCPort);
                });

            _usb.Removed += (s, e) => Dispatcher.Invoke(() =>
                {
                    UpdatePortList(cmbPIDPort);
                    UpdatePortList(cmbMFCPort);
                });

            _mfc.Closed += (s, e) => UpdateUI();
            _pid.Closed += (s, e) => UpdateUI();

            UpdateUI();
        }


        // Internal

        USB _usb = new USB();
        MFC _mfc = MFC.Instance;
        PID _pid = PID.Instance;


        private void UpdatePortList(ComboBox cmb)
        {
            var current = cmb.SelectedValue;

            cmb.Items.Clear();

            var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
            foreach (var port in availablePorts)
            {
                cmb.Items.Add(port);
            }

            if (current != null)
            {
                foreach (var item in availablePorts)
                {
                    if (item == current.ToString())
                    {
                        cmb.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void UpdateUI()
        {
            cmbPIDPort.IsEnabled = !_pid.IsOpen;
            cmbMFCPort.IsEnabled = !_mfc.IsOpen;

            btnPIDToggle.IsEnabled = cmbPIDPort.SelectedIndex >= 0;
            btnPIDToggle.Content = _pid.IsOpen ? "Close" : "Open";

            btnMFCToggle.IsEnabled = cmbMFCPort.SelectedIndex >= 0;
            btnMFCToggle.Content = _mfc.IsOpen ? "Close" : "Open";

            btnOdorProduction.IsEnabled = _pid.IsOpen && _mfc.IsOpen;
            btnThresholdTest.IsEnabled = _pid.IsOpen && _mfc.IsOpen;

            void EnableChildren(Panel panel, bool enable)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Control)
                    {
                        (child as Control).IsEnabled = _mfc.IsOpen;
                    }
                    else if (child is Panel)
                    {
                        EnableChildren(child as Panel, enable);
                    }
                }
            }

            EnableChildren(grdPlayground, _mfc.IsOpen);
        }

        private bool Toggle(CommPort port, string address)
        {
            bool success = true;

            if (!port.IsOpen)
            {
                var result = port.Start(address);
                success = result.Error == Error.Success;

                LogSource source = port.Name == "PID" ? LogSource.PID : LogSource.MFC;
                CommMonitor.Instance.LogResult(source, result);
            }
            else
            {
                port.Stop();
            }

            return success;
        }

        private void Close()
        {
            if (_pid.IsOpen)
            {
                _pid.Stop();
            }
            if (_mfc.IsOpen)
            {
                _mfc.Stop();
            }

            SaveSettings();
        }

        private void LoadSettings()
        {
            var settings = Properties.Settings.Default;
            txbFreshAir.Text = settings.Setup_MFC_FreshAir.ToString();
            txbOdor.Text = settings.Setup_MFC_Odor.ToString();
            cmbValve1.SelectedIndex = settings.Setup_MFC_Valve1;
            cmbValve2.SelectedIndex = settings.Setup_MFC_Valve2;

            foreach (string item in cmbMFCPort.Items)
            {
                if (item == settings.Setup_MFCPort)
                {
                    cmbMFCPort.SelectedItem = item;
                    break;
                }
            }

            foreach (string item in cmbPIDPort.Items)
            {
                if (item == settings.Setup_PIDPort)
                {
                    cmbPIDPort.SelectedItem = item;
                    break;
                }
            }
        }

        private void SaveSettings()
        {
            var settings = Properties.Settings.Default;
            try
            {
                settings.Setup_MFC_FreshAir = double.Parse(txbFreshAir.Text);
                settings.Setup_MFC_Odor = double.Parse(txbOdor.Text);
                settings.Setup_MFC_Valve1 = cmbValve1.SelectedIndex;
                settings.Setup_MFC_Valve2 = cmbValve2.SelectedIndex;
                settings.Setup_MFCPort = cmbMFCPort.SelectedItem?.ToString() ?? "";
                settings.Setup_PIDPort = cmbPIDPort.SelectedItem?.ToString() ?? "";
            }
            catch { }
            settings.Save();
        }

        // UI events

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (Focusable)
            {
                Focus();
            }
        }

        private void Page_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                cmbMFCPort.Items.Clear();
                cmbMFCPort.Items.Add("COM3");
                cmbMFCPort.SelectedIndex = 0;

                cmbPIDPort.Items.Clear();
                cmbPIDPort.Items.Add("COM4");
                cmbPIDPort.SelectedIndex = 0;

                _mfc.IsDebugging = true;
                _pid.IsDebugging = true;

                Storage.Instance.IsDebugging = true;
                lblDebug.Visibility = Visibility.Visible;
            }
        }

        private void cmbPort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUI();
        }

        private void btnMFCToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!Toggle(_mfc, (string)cmbMFCPort.SelectedItem))
            {
                MessageBox.Show("Cannot open the port");
            }

            UpdateUI();
        }

        private void btnPIDToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!Toggle(_pid, (string)cmbPIDPort.SelectedItem))
            {
                MessageBox.Show("Cannot open the port");
            }

            UpdateUI();
        }

        private void btnSetFreshAir_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(txbFreshAir.Text, out double value))
            {
                _mfc.FreshAirSpeed = value;
            }
        }

        private void btnSetOdor_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(txbOdor.Text, out double value))
            {
                _mfc.OdorSpeed = value;
            }
        }

        private void btnSetDirection_Click(object sender, RoutedEventArgs e)
        {
            _mfc.OdorDirection = (cmbValve1.SelectedIndex, cmbValve2.SelectedIndex) switch
            {
                (0, 0) => MFC.OdorFlow.ToWasteAll,
                (0, 1) => MFC.OdorFlow.ToWasteAndUser,
                (1, 0) => MFC.OdorFlow.ToSystemAndWaste,
                (1, 1) => MFC.OdorFlow.ToSystemAndUser,
                _ => throw new NotImplementedException($"Direction '{cmbValve1.SelectedItem}-{cmbValve2.SelectedItem}' is not expected to be set")
            };
        }

        private void btnOdorProduction_Click(object sender, RoutedEventArgs e)
        {
            Next(this, Tests.Test.OdorProduction);
        }

        private void btnThresholdTest_Click(object sender, RoutedEventArgs e)
        {
            Next(this, Tests.Test.Threshold);
        }
    }
}
