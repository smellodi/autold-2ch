using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using AutOlD2Ch.Comm;
using AutOlD2Ch.Pages.LptController;
using AutOlD2Ch.Tests.Common;
using AutOlD2Ch.Utils;
using System.Runtime.ConstrainedExecution;

namespace AutOlD2Ch.Tests.LptController;

public class Procedure : IDisposable
{
    [Flags]
    public enum Stage
    {
        None = 0,
        Odor1Flow = 1,
        Odor2Flow = 2,
        OdorFlow = 4,
    }

    public event EventHandler<double> Data;
    public event EventHandler<string> Marker;
    public event EventHandler<Stage> StageChanged;
    public event EventHandler Finished;

    public Pulse CurrentPulse { get; private set; } = null;

    public Procedure()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;

        FlowLogger.Instance.Clear();

        _logger.Clear();

        _timer.Elapsed += (s, e) =>
        {
            _dispatcher.Invoke(() =>  // we let the timer to count further without waiting for the end of reading from serial ports
            {
                if (_pid.GetSample(out PIDSample pidSample).Error == Error.Success)
                {
                    _logger.Add(pidSample);
                    _monitor.LogData(LogSource.PID, pidSample);
                    Data?.Invoke(this, pidSample.PID);
                }
                if (_mfc.GetSample(out MFCSample mfcSample).Error == Error.Success)
                {
                    _logger.Add(mfcSample);
                    _monitor.LogData(LogSource.MFC, mfcSample);
                }
            });
        };
    }

    public void Start(Settings settings)
    {
        _settings = settings;

        try { _lptPort = LptPort.GetPorts()[_settings.LptPort]; } catch {  }
        try {
            _comPort = new System.IO.Ports.SerialPort(_settings.ComPort)
            {
                StopBits = System.IO.Ports.StopBits.One,
                Parity = System.IO.Ports.Parity.None,
                Handshake = System.IO.Ports.Handshake.None,
                BaudRate = 115200,
                DataBits = 8,
            };
            _comPort.Open();
        } catch { }
        _lptPortReadingCancellationTokenSource = new();
        _marker = _lptPort?.ReadData() ?? 0;

        // catch window closing event, so we do not display termination message due to MFC comm closed
        Application.Current.MainWindow.Closing += MainWindow_Closing;

        // If the test is in progress, display warning message and quit the app:
        // Or can we recover here by trying to open the COM port again?
        _mfc.Closed += MFC_Closed;
        _pid.Closed += PID_Closed;

        var updateIntervalInSeconds = 0.001 * _settings.PIDReadingInterval;
        _monitor.MFCUpdateInterval = updateIntervalInSeconds;
        _monitor.PIDUpdateInterval = updateIntervalInSeconds;

        _mfc.FreshAirSpeed = _settings.FreshAir;
        _mfc.OdorDirection = MFC.ValvesOpened.None;
        _mfc.IsInShortPulseMode = false;

        _timer.Interval = _settings.PIDReadingInterval;
        _timer.AutoReset = true;
        _timer.Start();

        _logger.Start(_settings.PIDReadingInterval);

        _ = ReadPortStatus();
    }

    public void Stop()
    {
        StopTimers();

        _mfc.IsInShortPulseMode = false;
        _mfc.Odor1Speed = MFC.ODOR_MIN_SPEED;
        _mfc.Odor2Speed = MFC.ODOR_MIN_SPEED;
        _mfc.OdorDirection = MFC.ValvesOpened.None;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _runner?.Dispose();
        _pulseController?.Dispose();
        GC.SuppressFinalize(this);

        if (_comPort != null)
        {
            try { _comPort.Close(); }                               // Port were in use? Simply close handle.
            finally { _comPort = null; }
        }
    }

    // Internal

    const short MARKER_FINISHED = 255;
    const short LPT_READING_INTERVAL = 50;  // ms

    readonly MFC _mfc = MFC.Instance;
    readonly PID _pid = PID.Instance;
    readonly SyncLogger _logger = SyncLogger.Instance;
    readonly CommMonitor _monitor = CommMonitor.Instance;
    readonly System.Timers.Timer _timer = new();
    readonly Dispatcher _dispatcher;

    Settings _settings;

    DispatchOnce _runner;
    PulsesController _pulseController;
    System.IO.Ports.SerialPort _comPort;
    LptPort _lptPort;
    CancellationTokenSource _lptPortReadingCancellationTokenSource;
    short _marker;

    private async Task ReadPortStatus()
    {
        try
        {
            await Task.Delay(LPT_READING_INTERVAL, _lptPortReadingCancellationTokenSource.Token);
            if (_runner == null)
            {
                CheckMarker();
            }
            _ = ReadPortStatus();
        }
        catch (OperationCanceledException)
        {
            Application.Current.Shutdown();
        }
    }

    Random _rnd = new();
    private void CheckMarker()
    {
        var data = _lptPort?.ReadData() ?? (short)(_rnd.NextDouble() < 0.002 ? 1 : (_rnd.NextDouble() < 0.005 ? 2 : 0));
        if (data != _marker)
        {
            _marker = data;
            if (_marker == MARKER_FINISHED)
            {
                FinalizeMarker();
            }
            else if (_marker > 0)
            {
                if (_comPort != null)
                {
                    var buffer = new byte[] { (byte)_marker, 0 };
                    _comPort.Write(buffer, 0, buffer.Length);
                }

                UseOdor(_marker);
            }
        }
    }

    private void UseOdor(int id)
    {
        if (_settings.Pulses.TryGetValue(id, out Pulse pulse))
        {
            Marker?.Invoke(this, _marker.ToString());

            CurrentPulse = pulse;

            _logger.Add($"S{pulse.Channel1?.Flow ?? 0}/{pulse.Channel2?.Flow ?? 0}");

            _runner = DispatchOnce
                .Do(0.1, StartOdorFlow)
                .Then(_settings.OdorFlowDuration, StopOdorFlow);
        }
        else
        {
            Marker?.Invoke(this, $"{id} [unknown odor]");
        }
    }

    private void StartOdorFlow()
    {
        _pulseController = new PulsesController(CurrentPulse, _settings.OdorFlowDurationMs);
        _pulseController.PulseStateChanged += PulseStateChanged;
        _pulseController.Run();
    }

    private void StopOdorFlow()
    {
        if (_mfc.OdorDirection != MFC.ValvesOpened.None)
        {
            _mfc.OdorDirection = MFC.ValvesOpened.None;
            _logger.Add("V" + ((int)MFC.ValvesOpened.None).ToString("D2"));
        }

        CurrentPulse = null;

        StageChanged?.Invoke(this, Stage.None);

        _runner?.Dispose();
        _runner = null;
    }

    private void FinalizeMarker()
    {
        _dispatcher.Invoke(() =>
        {
            Application.Current.MainWindow.Closing -= MainWindow_Closing;
            _mfc.Closed -= MFC_Closed;
            _pid.Closed -= PID_Closed;
        });

        Finished?.Invoke(this, new EventArgs());
    }

    private void ExitOnDeviceError(string source)
    {
        if (_timer.Enabled)
        {
            StopTimers();

            MsgBox.Error(
                App.Name + " - " + L10n.T("OdorPulses"),
                string.Format(L10n.T("DeviceConnLost"), source) + " " + L10n.T("AppTerminated"));
            Application.Current.Shutdown();
        }
    }

    private void StopTimers()
    {
        _runner?.Stop();
        _timer.Stop();
        _pulseController?.Terminate();
    }

    // Event handlers

    private void PulseStateChanged(object sender, PulsesController.PulseStateChangedEventArgs e)
    {
        if (e.IsLast)
        {
            _pulseController = null;

            if (_mfc.OdorDirection != MFC.ValvesOpened.None)
            {
                StageChanged?.Invoke(this, Stage.OdorFlow);
            }

            return;
        }

        Stage newStage = Stage.None;
        foreach (var startingChannel in e.StartingChannels)
        {
            newStage |= startingChannel.ID == 1 ? Stage.Odor1Flow : Stage.Odor2Flow;
        }
        foreach (var ongoingChannel in e.OngoingChannels)
        {
            newStage |= ongoingChannel.ID == 1 ? Stage.Odor1Flow : Stage.Odor2Flow;
        }

        MFC.ValvesOpened valves = newStage switch
        {
            Stage.Odor1Flow => MFC.ValvesOpened.Valve1,
            Stage.Odor2Flow => MFC.ValvesOpened.Valve2,
            (Stage.Odor1Flow | Stage.Odor2Flow) => MFC.ValvesOpened.Valve1 | MFC.ValvesOpened.Valve2,
            _ => MFC.ValvesOpened.None
        };

        _logger.Add("V" + ((int)valves).ToString("D2"));

        foreach (var startingChannel in e.StartingChannels)
        {
            _mfc.StartPulse(
                startingChannel.Valve,
                startingChannel.GetDuration(_settings.OdorFlowDurationMs));
        }

        _mfc.OdorDirection = valves;

        StageChanged?.Invoke(this, newStage | Stage.OdorFlow);
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        StopTimers();
    }

    private void MFC_Closed(object sender, EventArgs e)
    {
        ExitOnDeviceError("MFC");
    }

    private void PID_Closed(object sender, EventArgs e)
    {
        ExitOnDeviceError("PID");
    }
}
