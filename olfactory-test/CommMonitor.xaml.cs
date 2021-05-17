using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Olfactory
{
    public partial class CommMonitor : Window
    {
        DispatcherTimer _mfcTimer = new DispatcherTimer();
        DispatcherTimer _pidTimer = new DispatcherTimer();

        MFC _mfc = MFC.Instance;
        PID _pid = PID.Instance;

        bool _preventClosing = true;

        public CommMonitor()
        {
            InitializeComponent();

            Application.Current.Exit += (s, e) => _preventClosing = false;

            _mfc.RequestResult += (s, e) => LogResult(_mfc.Name, e);
            _mfc.Message += (s, e) => LogMessage(_mfc.Name, e);
            _mfc.Closed += (s, e) => {
                LogResult(_mfc.Name, new Result() { Error = Error.Success, Reason = "Stopped" });
            };

            _pid.RequestResult += (s, e) => LogResult(_pid.Name, e);
            _pid.Closed += (s, e) => {
                LogResult(_pid.Name, new Result() { Error = Error.Success, Reason = "Stopped" });
            };

            _mfcTimer.Interval = TimeSpan.FromSeconds(1);
            _mfcTimer.Tick += (s, e) => {
                if (_mfc.IsOpen)
                {
                    var result = _mfc.GetSample(out MFCSample sample);
                    Log(txbMFC, _mfc.Name, result, sample.ToString('\t'));
                }
            };

            _pidTimer.Interval = TimeSpan.FromSeconds(1);
            _pidTimer.Tick += (s, e) => {
                if (_pid.IsOpen)
                {
                    var result = _pid.GetSample(out PIDSample sample);
                    Log(txbPID, _pid.Name, result, sample);
                }
            };

            txbMFC.Text = string.Join('\t', _mfc.DataColumns) + "\r\n";
            txbPID.Text = string.Join('\t', _pid.DataColumns) + "\r\n";
        }

        public void LogResult(string tag, Result result)
        {
            txbDebug.AppendText($"{CommPort.Timestamp} [{tag}] {result}\r\n");
            txbDebug.ScrollToEnd();
        }

        public void LogMessage(string tag, string message)
        {
            txbDebug.AppendText($"{CommPort.Timestamp} [{tag}] {message}\r\n");
            txbDebug.ScrollToEnd();
        }


        // Internal

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

        private void Log(TextBox output, string tag, Result result, object data)
        {
            if (result.Error == Error.Success)
            {
                output.AppendText(data.ToString() + "\r\n");
            }
            else
            {
                output.AppendText($"{CommPort.Timestamp} ----- INVALID -----\r\n");
                LogResult(tag, result);
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
        }

        private void OnClearPID_Click(object sender, RoutedEventArgs e)
        {
            txbPID.Clear();
            txbPID.Text = string.Join('\t', _pid.DataColumns) + "\r\n";
        }

        private void chkMFCMonitor_Checked(object sender, RoutedEventArgs e)
        {
            ToggleMonitoring(e.Source as CheckBox, _mfcTimer);
        }

        private void chkPIDMonitor_Checked(object sender, RoutedEventArgs e)
        {
            ToggleMonitoring(e.Source as CheckBox, _pidTimer);
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
