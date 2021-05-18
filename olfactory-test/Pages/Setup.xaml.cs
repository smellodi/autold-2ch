using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Olfactory.Pages
{
    public partial class Setup : Page, IPage<Test.Tests>
    {
        public event EventHandler<Test.Tests> Next = delegate { };
        public event EventHandler<Result> LogResult = delegate { };

        USB _usb = new USB();
        MFC _mfc = MFC.Instance;
        PID _pid = PID.Instance;

        public Setup()
        {
            InitializeComponent();

            UpdatePortList(cmbPIDPort);
            UpdatePortList(cmbMFCPort);

            Application.Current.Exit += (s, e) => Close();

            _usb.Inserted += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdatePortList(cmbPIDPort);
                    UpdatePortList(cmbMFCPort);
                });
            };
            _usb.Removed += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdatePortList(cmbPIDPort);
                    UpdatePortList(cmbMFCPort);
                });
            };

            _mfc.Closed += (s, e) => {
                UpdateUI();
            };
            _pid.Closed += (s, e) => {
                UpdateUI();
            };

            UpdateUI();
        }


        // Internal

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

            btnNext.IsEnabled = _pid.IsOpen && _mfc.IsOpen;

            foreach (var child in grdPlayground.Children)
            {
                (child as Control).IsEnabled = _mfc.IsOpen;
            }
        }

        private bool Toggle(CommPort port, string address)
        {
            bool success = true;

            if (!port.IsOpen)
            {
                var result = port.Start(address);
                success = result.Error == Error.Success;
                LogResult(port.Name, result);
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
        }


        // UI events

        private void On_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1)
            {
                cmbPIDPort.Items.Clear();
                cmbPIDPort.Items.Add("COM4");
                cmbPIDPort.SelectedIndex = 0;

                _pid.IsDebugging = true;
            }
            else if (e.Key == Key.F2)
            {
                cmbMFCPort.Items.Clear();
                cmbMFCPort.Items.Add("COM3");
                cmbMFCPort.SelectedIndex = 0;

                _mfc.IsDebugging = true;
            }
        }

        private void OnPort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUI();
        }

        private void OnMFCToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!Toggle(_mfc, (string)cmbMFCPort.SelectedItem))
            {
                MessageBox.Show("Cannot open the port");
            }

            UpdateUI();
        }

        private void OnPIDToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!Toggle(_pid, (string)cmbPIDPort.SelectedItem))
            {
                MessageBox.Show("Cannot open the port");
            }

            UpdateUI();
        }

        private void OnThresholdTest_Click(object sender, RoutedEventArgs e)
        {
            Next(this, Test.Tests.Threshold);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (Focusable)
            {
                Focus();
            }
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
            _mfc.OdorDirection = cmbDirection.SelectedIndex switch
            {
                0 => MFC.OdorFlow.None,
                1 => MFC.OdorFlow.ToWaste,
                2 => MFC.OdorFlow.ToUser,
                _ => throw new NotImplementedException($"Direction '{cmbDirection.SelectedItem}' is not expected to be set")
            };
        }
    }
}
