using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Olfactory.Comm;
using Olfactory.Utils;

namespace Olfactory
{
    public partial class CommMonitor : Window
    {
        public static CommMonitor Instance { get; private set; }

        public CommMonitor()
        {
            InitializeComponent();

            Instance = this;

            Storage.Instance.BindScaleToZoomLevel(sctScale);

            Application.Current.Exit += (s, e) => _preventClosing = false;

            _mfc.RequestResult += (s, e) => LogResult(LogSource.MFC, e);
            _mfc.Message += (s, e) => LogMessage(LogSource.MFC, e);
            _mfc.Closed += (s, e) => {
                LogResult(LogSource.MFC, new Result() { Error = Error.Success, Reason = "Stopped" });
            };

            _pid.RequestResult += (s, e) => LogResult(LogSource.PID, e);
            _pid.Closed += (s, e) => {
                LogResult(LogSource.PID, new Result() { Error = Error.Success, Reason = "Stopped" });
            };

            _mfcTimer.Interval = TimeSpan.FromSeconds(1);
            _mfcTimer.Tick += (s, e) => {
                if (_mfc.IsOpen)
                {
                    var result = _mfc.GetSample(out MFCSample sample);
                    Log(txbMFC, LogSource.MFC, result, sample);
                }
            };

            _pidTimer.Interval = TimeSpan.FromSeconds(1);
            _pidTimer.Tick += (s, e) => {
                if (_pid.IsOpen)
                {
                    var result = _pid.GetSample(out PIDSample sample);
                    Log(txbPID, LogSource.PID, result, sample);
                }
            };

            txbMFC.Text = string.Join('\t', _mfc.DataColumns) + "\r\n";
            txbPID.Text = string.Join('\t', _pid.DataColumns) + "\r\n";

            var settings = Properties.Settings.Default;
            if (settings.MonitorWindow_Width > 0)
            {
                Left = settings.MonitorWindow_X;
                Top = settings.MonitorWindow_Y;
                Width = settings.MonitorWindow_Width;
                Height = settings.MonitorWindow_Height;
            }

            UpdateUI();
        }

        public void LogResult(LogSource source, Result result)
        {
            _logger.Add(source, "cmnd", result.ToString());
            txbDebug.AppendText($"{Utils.Timestamp.Value} [{source}] {result}\r\n");
            txbDebug.ScrollToEnd();
        }

        public void LogMessage(LogSource source, string message)
        {
            _logger.Add(source, "fdbk", message);
            txbDebug.AppendText($"{Utils.Timestamp.Value} [{source}] {message}\r\n");
            txbDebug.ScrollToEnd();
        }

        public void LogData(LogSource source, object data)
        {
            TextBox output = source switch
            {
                LogSource.MFC => txbMFC,
                LogSource.PID => txbPID,
                _ => txbDebug
            };
            output.AppendText(data.ToString() + "\r\n");
            output.ScrollToEnd();

            AddToList(source, data);
        }


        // Internal

        DispatcherTimer _mfcTimer = new DispatcherTimer();
        DispatcherTimer _pidTimer = new DispatcherTimer();

        MFC _mfc = MFC.Instance;
        PID _pid = PID.Instance;

        FlowLogger _logger = FlowLogger.Instance;

        bool _preventClosing = true;

        private void AddToList(LogSource source, object data)
        {
            var list = source switch
            {
                LogSource.MFC => lsvMFC,
                LogSource.PID => lsvPID,
                _ => null
            };

            if (list != null)
            {
                list.Items.Add(data);
                list.ScrollIntoView(data);
            }
        }

        private void ToggleMonitoring(CheckBox chk, DispatcherTimer timer)
        {
            if (chk.IsChecked ?? false)
            {
                timer.Start();
            }
            else
            {
                timer.Stop();
            }
        }

        private void Log(TextBox output, LogSource source, Result result, object data)
        {
            if (result.Error == Error.Success)
            {
                _logger.Add(source, "data", data.ToString());
                output.AppendText(data.ToString() + "\r\n");
                AddToList(source, data);
                UpdateUI();
            }
            else
            {
                output.AppendText($"{Utils.Timestamp.Value} ----- INVALID -----\r\n");
                LogResult(source, result);
            }

            output.ScrollToEnd();
        }

        private void UpdateUI()
        {
            btnMFCSave.IsEnabled = _logger.HasMeasurements(LogSource.MFC);
            btnMFCClear.IsEnabled = lsvMFC.Items.Count > 0;

            btnPIDSave.IsEnabled = _logger.HasMeasurements(LogSource.PID);
            btnPIDClear.IsEnabled = lsvPID.Items.Count > 0;
        }

        // UI events

        private void OnClearDebug_Click(object sender, RoutedEventArgs e)
        {
            txbDebug.Clear();
        }

        private void OnClearMFC_Click(object sender, RoutedEventArgs e)
        {
            txbMFC.Clear();
            txbMFC.Text = string.Join('\t', _mfc.DataColumns) + "\r\n";
            lsvMFC.Items.Clear();

            UpdateUI();
        }

        private void OnSaveMFC_Click(object sender, RoutedEventArgs e)
        {
            _logger.SaveOnly(LogSource.MFC, "data", $"MFC_{DateTime.Now:u}.txt".ToPath());
        }

        private void OnClearPID_Click(object sender, RoutedEventArgs e)
        {
            txbPID.Clear();
            txbPID.Text = string.Join('\t', _pid.DataColumns) + "\r\n";
            lsvPID.Items.Clear();

            UpdateUI();
        }

        private void OnSavePID_Click(object sender, RoutedEventArgs e)
        {
            _logger.SaveOnly(LogSource.PID, "data", $"MFC_{DateTime.Now:u}.txt".ToPath());
        }

        private void chkMFCMonitor_Checked(object sender, RoutedEventArgs e)
        {
            ToggleMonitoring(e.Source as CheckBox, _mfcTimer);
        }

        private void chkMFCAsText_Checked(object sender, RoutedEventArgs e)
        {
            lsvMFC.Visibility = chkMFCAsText.IsChecked ?? false ? Visibility.Hidden : Visibility.Visible;
            txbMFC.Visibility = chkMFCAsText.IsChecked ?? false ? Visibility.Visible : Visibility.Hidden;
        }

        private void chkPIDMonitor_Checked(object sender, RoutedEventArgs e)
        {
            ToggleMonitoring(e.Source as CheckBox, _pidTimer);
        }

        private void chkPIDAsText_Checked(object sender, RoutedEventArgs e)
        {
            lsvPID.Visibility = chkPIDAsText.IsChecked ?? false ? Visibility.Hidden : Visibility.Visible;
            txbPID.Visibility = chkPIDAsText.IsChecked ?? false ? Visibility.Visible : Visibility.Hidden;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Hide();

            var settings = Properties.Settings.Default;
            settings.MonitorWindow_X = Left;
            settings.MonitorWindow_Y = Top;
            settings.MonitorWindow_Width = Width;
            settings.MonitorWindow_Height = Height;
            settings.Save();

            e.Cancel = _preventClosing;
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            chkMFCMonitor.IsEnabled = _mfc.IsOpen;
            chkPIDMonitor.IsEnabled = _pid.IsOpen;
            UpdateUI();
        }
    }
}
