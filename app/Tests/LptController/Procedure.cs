using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using AutOlD2Ch.Comm;
using AutOlD2Ch.Pages.LptController;
using AutOlD2Ch.Tests.Common;
using AutOlD2Ch.Utils;
using System.Linq;

namespace AutOlD2Ch.Tests.LptController;

public class Procedure : IDisposable
{
    [Flags]
    public enum Stage
    {
        None = 0,
        Odor1Flow = 1,
        Odor2Flow = 2,
        OdorFlow = 4,   /// this flag must be present when stage includes <see cref="Stage.Odor1Flow"/> or <see cref="Stage.Odor2Flow"/>
    }

    public event EventHandler<double>? Data;
    public event EventHandler<string>? Marker;
    public event EventHandler<Stage>? StageChanged;
    public event EventHandler? Finished;

    public Pulse? CurrentPulse { get; private set; } = null;

    public Procedure()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;

        _eventLogger.Clear();
        _dataLogger.Clear();

        _timer.Elapsed += (s, e) =>
        {
            _dispatcher.Invoke(() =>  // we let the timer to count further without waiting for the end of reading from serial ports
            {
                if (_pid.GetSample(out PIDSample pidSample).Error == Error.Success)
                {
                    _dataLogger.Add(pidSample);
                    _monitor?.LogData(LogSource.PID, pidSample);
                    Data?.Invoke(this, pidSample.PID);
                }
                if (_mfc.GetSample(out MFCSample mfcSample).Error == Error.Success)
                {
                    _dataLogger.Add(mfcSample);
                    _monitor?.LogData(LogSource.MFC, mfcSample);
                }
            });
        };
    }

    public void Start(Settings settings)
    {
        _settings = settings;

        try { _lptPort = LptPort.GetPorts(out string[] _)[_settings.LptPort]; } catch {  }
        try { _comPort = new ComPort(_settings.ComPort); } catch { }

        _lptPortReadingCancellationTokenSource = new();
        _marker = _lptPort?.ReadData() ?? 0;

        // catch window closing event, so we do not display termination message due to MFC comm closed
        Application.Current.MainWindow.Closing += MainWindow_Closing;

        // If the test is in progress, display warning message and quit the app:
        // Or can we recover here by trying to open the COM port again?
        _mfc.Closed += MFC_Closed;
        _pid.Closed += PID_Closed;

        var updateIntervalInSeconds = 0.001 * _settings.PIDReadingInterval;
        if (_monitor != null)
        {
            _monitor.MFCUpdateInterval = updateIntervalInSeconds;
            _monitor.PIDUpdateInterval = updateIntervalInSeconds;
        }

        _mfc.FreshAirSpeed = _settings.FreshAir;
        _mfc.OdorDirection = MFC.ValvesOpened.None;
        _mfc.IsInShortPulseMode = false;

        _timer.Interval = _settings.PIDReadingInterval;
        _timer.AutoReset = true;
        _timer.Start();

        _dataLogger.Start(_settings.PIDReadingInterval);

        if (_lptPort == null)
        {
            var markers = _settings.Pulses.Select(kv => (short)kv.Key).ToList();
            markers.Add(MARKER_TOBII_FINISHED);
            EmulatedMarkers = markers.ToArray();
        }

        if (_lptPort != null || Storage.Instance.IsDebugging)
        {
            _ = ReadPortStatus();
        }
    }

    public void Stop()
    {
        StopTimers();
        _lptPortReadingCancellationTokenSource.Cancel();

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

        _comPort?.Dispose();
    }

    // Internal

    const short MARKER_TOBII_FINISHED = 255;
    const byte MARKER_NEXUS_FINISHED = (byte)'#';
    const short LPT_READING_INTERVAL = 50;  // ms

    readonly MFC _mfc = MFC.Instance;
    readonly PID _pid = PID.Instance;
    readonly SyncLogger _dataLogger = SyncLogger.Instance;
    readonly FlowLogger _eventLogger = FlowLogger.Instance;
    readonly CommMonitor? _monitor = CommMonitor.Instance;
    readonly System.Timers.Timer _timer = new();
    readonly Dispatcher _dispatcher;

    Settings? _settings;

    DispatchOnce? _runner;
    PulsesController? _pulseController;
    ComPort? _comPort;
    LptPort? _lptPort;

    CancellationTokenSource _lptPortReadingCancellationTokenSource = new();
    short _marker;

    short[] EmulatedMarkers = Array.Empty<short>();

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
        catch (OperationCanceledException) { }
    }

    private void CheckMarker()
    {
        var data = _lptPort?.ReadData() ?? LptPort.Emulate(EmulatedMarkers);
        if (data != _marker)
        {
            _marker = data;
            _eventLogger.Add(LogSource.LptCtrl, "Marker", _marker.ToString());

            if (_marker == MARKER_TOBII_FINISHED)
            {
                _comPort?.SendMarker(TobiiToNexus(_marker));
                _lptPortReadingCancellationTokenSource.Cancel();
                FinalizeMarker();
            }
            else if (_marker > 0)
            {
                _comPort?.SendMarker(TobiiToNexus(_marker));
                UseOdor(_marker);
            }
        }
    }

    private void UseOdor(int id)
    {
        if (_settings!.Pulses.TryGetValue(id, out Pulse? pulse))
        {
            Marker?.Invoke(this, _marker.ToString());

            CurrentPulse = pulse;

            _dataLogger.Add($"S{pulse.Channel1?.Flow ?? 0}/{pulse.Channel2?.Flow ?? 0}");

            _runner = DispatchOnce
                .Do(0.1, StartOdorFlow)?
                .Then(_settings.OdorFlowDuration, StopOdorFlow);
        }
        else
        {
            Marker?.Invoke(this, $"{id} [unknown odor]");
        }
    }

    private void StartOdorFlow()
    {
        if (CurrentPulse == null)
            return;

        _pulseController = new PulsesController(CurrentPulse, _settings!.OdorFlowDurationMs);
        _pulseController.PulseStateChanged += PulseStateChanged;
        _pulseController.Run();
    }

    private void StopOdorFlow()
    {
        if (_mfc.OdorDirection != MFC.ValvesOpened.None)
        {
            _mfc.OdorDirection = MFC.ValvesOpened.None;
            _dataLogger.Add("V" + ((int)MFC.ValvesOpened.None).ToString("D2"));
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

        _dataLogger.Add("F");

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

    private static byte TobiiToNexus(short marker)
    {
        // return (byte)marker;

        // We convert the Tobii's binary marker to an ASCII marker to be send to NeXus Trigger:
        // either '1'-'9' if the the Tobii's marker is 1-9,
        // or 'A'-'Z' if the Tobii's marker is 10-35
        // or 'a'-'z' if the Tobii's marker is 36-61
        return (byte)(marker switch
        {
            < 10 => marker + 0x30,
            < 36 => (marker - 10) + 0x41,
            < 62 => (marker - 36) + 0x61,
            MARKER_TOBII_FINISHED => MARKER_NEXUS_FINISHED,
            _ => 0
        });
    }

    // Event handlers

    private void PulseStateChanged(object? sender, PulsesController.PulseStateChangedEventArgs e)
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

        _dataLogger.Add("V" + ((int)valves).ToString("D2"));

        foreach (var startingChannel in e.StartingChannels)
        {
            _mfc.StartPulse(
                startingChannel.Valve,
                startingChannel.GetDuration(_settings!.OdorFlowDurationMs));
        }

        _mfc.OdorDirection = valves;

        StageChanged?.Invoke(this, newStage | Stage.OdorFlow);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        StopTimers();
    }

    private void MFC_Closed(object? sender, EventArgs e)
    {
        ExitOnDeviceError("MFC");
    }

    private void PID_Closed(object? sender, EventArgs e)
    {
        ExitOnDeviceError("PID");
    }
}
