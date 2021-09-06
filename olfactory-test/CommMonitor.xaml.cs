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

            _mfc.CommandResult += (s, e) => LogResult(LogSource.MFC, e.Result, e.Command, e.Value);
            _mfc.Message += (s, e) => LogMessage(LogSource.MFC, e);
            _mfc.Closed += (s, e) => {
                LogResult(LogSource.MFC, new Result() { Error = Error.Success, Reason = "Stopped" });
            };

            _pid.RequestResult += (s, e) => LogResult(LogSource.PID, e);
            _pid.Closed += (s, e) => {
                LogResult(LogSource.PID, new Result() { Error = Error.Success, Reason = "Stopped" });
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

        public void LogResult(LogSource source, Result result, params string[] data)
        {
            Dispatcher.Invoke(() =>
            {
                if (result.Error == Error.Success && source == LogSource.MFC)
                {
                    _logger.Add(source, "cmnd", data);
                }
                else
                {
                    _logger.Add(source, "cmnd", result.ToString());
                }
                txbDebug.AppendText($"{Timestamp.Ms} [{source}] {result}\r\n");
                txbDebug.ScrollToEnd();
            });
        }

        public void LogData(LogSource source, ISample data)
        {
            Dispatcher.Invoke(() =>
            {
                AddToList(source, data);
            });
        }


        // Internal

        MFC _mfc = MFC.Instance;
        PID _pid = PID.Instance;

        FlowLogger _logger = FlowLogger.Instance;

        bool _preventClosing = true;

        private void AddToList(LogSource source, ISample data)
        {
            // add to text
            TextBox output = source switch
            {
                LogSource.MFC => txbMFC,
                LogSource.PID => txbPID,
                _ => txbDebug
            };
            output.AppendText(data.ToString() + "\r\n");
            output.ScrollToEnd();

            // add to table
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

            // add to graph
            if (source == LogSource.MFC)
            {
                lmsMFC.Add(
                    (double)data.Time / 1000,
                    data.MainValue
                    //Controls.LiveMeasurement.OdorColor(_mfc.OdorDirection)
                );
            }
            else if (source == LogSource.PID)
            {
                lmsPID.Add((double)data.Time / 1000, data.MainValue);
            }
        }

        private void LogMessage(LogSource source, string message)
        {
            _logger.Add(source, "fdbk", message);
            txbDebug.AppendText($"{Timestamp.Ms} [{source}] {message}\r\n");
            txbDebug.ScrollToEnd();
        }

        private void UpdateUI()
        {
            btnMFCSave.IsEnabled = _logger.HasMeasurements(LogSource.MFC);
            btnMFCClear.IsEnabled = lsvMFC.Items.Count > 0;

            btnPIDSave.IsEnabled = _logger.HasMeasurements(LogSource.PID);
            btnPIDClear.IsEnabled = lsvPID.Items.Count > 0;
        }

        // UI events

        private void btnClearDebug_Click(object sender, RoutedEventArgs e)
        {
            txbDebug.Clear();
        }

        private void btnClearMFC_Click(object sender, RoutedEventArgs e)
        {
            txbMFC.Clear();
            txbMFC.Text = string.Join('\t', _mfc.DataColumns) + "\r\n";
            lsvMFC.Items.Clear();
            lmsMFC.Reset(/*Controls.LiveMeasurement.OdorColor(_mfc.OdorDirection)*/);

            UpdateUI();
        }

        private void btnSaveMFC_Click(object sender, RoutedEventArgs e)
        {
            _logger.SaveOnly(LogSource.MFC, "data", $"MFC_{DateTime.Now:u}.txt".ToPath());
        }

        private void btnClearPID_Click(object sender, RoutedEventArgs e)
        {
            txbPID.Clear();
            txbPID.Text = string.Join('\t', _pid.DataColumns) + "\r\n";
            lsvPID.Items.Clear();
            lmsPID.Reset(/*Controls.LiveMeasurement.BRUSH_NEUTRAL*/);

            UpdateUI();
        }

        private void btnSavePID_Click(object sender, RoutedEventArgs e)
        {
            _logger.SaveOnly(LogSource.PID, "data", $"PID_{DateTime.Now:u}.txt".ToPath());
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            lmsMFC.Reset(/*Controls.LiveMeasurement.OdorColor(_mfc.OdorDirection)*/);
            lmsPID.Reset(/*Controls.LiveMeasurement.BRUSH_NEUTRAL*/);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            UpdateUI();
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
    }
}
