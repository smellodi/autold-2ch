using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Olfactory.Comm;

namespace Olfactory
{
    public partial class CommMonitor : Window
    {
        public static CommMonitor Instance { get; private set; }

        DispatcherTimer _mfcTimer = new DispatcherTimer();
        DispatcherTimer _pidTimer = new DispatcherTimer();

        MFC _mfc = MFC.Instance;
        PID _pid = PID.Instance;

        Logger _logger = Logger.Instance;

        bool _preventClosing = true;


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
            }
            else
            {
                output.AppendText($"{Utils.Timestamp.Value} ----- INVALID -----\r\n");
                LogResult(source, result);
            }

            output.ScrollToEnd();
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
        }

        private void OnClearPID_Click(object sender, RoutedEventArgs e)
        {
            txbPID.Clear();
            txbPID.Text = string.Join('\t', _pid.DataColumns) + "\r\n";
            lsvPID.Items.Clear();
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
            e.Cancel = _preventClosing;
            Hide();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            chkMFCMonitor.IsEnabled = _mfc.IsOpen;
            chkPIDMonitor.IsEnabled = _pid.IsOpen;
        }
    }
}
