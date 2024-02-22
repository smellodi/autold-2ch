using AutOlD2Ch.Comm;
using AutOlD2Ch.Utils;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AutOlD2Ch;

public partial class CommMonitor : Window
{
    public static CommMonitor? Instance { get; private set; }

    /// <summary>
    /// Update interval in seconds
    /// </summary>
    public double MFCUpdateInterval { get; set; } = 1;
    /// <summary>
    /// Update interval in seconds
    /// </summary>
    public double PIDUpdateInterval { get; set; } = 1;

    public CommMonitor()
    {
        InitializeComponent();

        Instance = this;

        Storage.Instance.BindScaleToZoomLevel(sctScale);

        Application.Current.Exit += (s, e) => _preventClosing = false;

        _mfc.CommandResult += (s, e) => LogResult(LogSource.MFC, e.Result, e.Command, e.Value);
        _mfc.Message += (s, e) => LogMessage(LogSource.MFC, e);
        _mfc.Closed += (s, e) =>
        {
            LogResult(LogSource.MFC, new Result() { Error = Error.Success, Reason = "Stopped" });
        };

        _pid.RequestResult += (s, e) => LogResult(LogSource.PID, e);
        _pid.Closed += (s, e) =>
        {
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

    readonly MFC _mfc = MFC.Instance;
    readonly PID _pid = PID.Instance;

    readonly FlowLogger _logger = FlowLogger.Instance;

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
            lmsMFC.Add(0.001 * data.Time, data.MainValue);
        }
        else if (source == LogSource.PID)
        {
            lmsPID.Add(0.001 * data.Time, data.MainValue);
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

    private void ClearDebug_Click(object sender, RoutedEventArgs e)
    {
        txbDebug.Clear();
    }

    private void ClearMFC_Click(object sender, RoutedEventArgs e)
    {
        txbMFC.Clear();
        txbMFC.Text = string.Join('\t', _mfc.DataColumns) + "\r\n";
        lsvMFC.Items.Clear();
        lmsMFC.Reset(MFCUpdateInterval /*Controls.LiveMeasurement.OdorColor(_mfc.OdorDirection)*/);

        UpdateUI();
    }

    private void SaveMFC_Click(object sender, RoutedEventArgs e)
    {
        _logger.SaveOnly(LogSource.MFC, "data", $"MFC_{DateTime.Now:u}.txt".ToPath());
    }

    private void ClearPID_Click(object sender, RoutedEventArgs e)
    {
        txbPID.Clear();
        txbPID.Text = string.Join('\t', _pid.DataColumns) + "\r\n";
        lsvPID.Items.Clear();
        lmsPID.Reset(PIDUpdateInterval /*Controls.LiveMeasurement.BRUSH_NEUTRAL*/);

        UpdateUI();
    }

    private void SavePID_Click(object sender, RoutedEventArgs e)
    {
        _logger.SaveOnly(LogSource.PID, "data", $"PID_{DateTime.Now:u}.txt".ToPath());
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        lmsMFC.Reset(MFCUpdateInterval /*Controls.LiveMeasurement.OdorColor(_mfc.OdorDirection)*/);
        lmsPID.Reset(PIDUpdateInterval /*Controls.LiveMeasurement.BRUSH_NEUTRAL*/);
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
