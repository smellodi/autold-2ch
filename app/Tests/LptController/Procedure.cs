using System;
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
        OdorFlow = 4,   /// this flag must be present when the stage includes <see cref="Stage.Odor1Flow"/> or <see cref="Stage.Odor2Flow"/>
    }

    public event EventHandler<string>? Marker;
    public event EventHandler<Stage>? StageChanged;
    public event EventHandler? Finished;

    public Pulse? CurrentPulse { get; private set; } = null;

    public Procedure()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;

        _eventLogger.Clear();
    }

    public void Start(Settings settings)
    {
        _settings = settings;

        try { _lptPort = LptPort.GetPorts()[_settings.LptPort]; } catch {  }
        try { _comPort = new ComPort(_settings.ComPort); } catch { }

        _marker = _lptPort?.ReadData() ?? 0;

        // catch window closing event, so we do not display termination message due to MFC comm closed
        Application.Current.MainWindow.Closing += MainWindow_Closing;

        // If the test is in progress, display warning message and quit the app:
        // Or can we recover here by trying to open the COM port again?
        _mfc.Closed += MFC_Closed;

        _mfc.FreshAirSpeed = _settings.FreshAir;
        _mfc.OdorDirection = MFC.ValvesOpened.None;
        _mfc.IsInShortPulseMode = false;
        _mfc.Odor1Speed = MFC_FLOW_BETWEEN_PULSES;
        _mfc.Odor2Speed = MFC_FLOW_BETWEEN_PULSES;

        _eventLogger.Add(LogSource.LptCtrl, "Session", "Start");

        if (_lptPort == null)
        {
            var markers = _settings.Pulses.Select(kv => (short)kv.Key).ToList();
            markers.Add(MARKER_TOBII_FINISHED);
            EmulatedMarkers = markers.ToArray();
        }

        //if (_lptPort != null || Storage.Instance.IsDebugging)
        {
            _lptReadingThread = new Thread(ReadPortStatus);
            _lptReadingThread.Start();
        }
    }

    public void Stop(bool isCalledByMarker = false)
    {
        StopProcesses();

        _eventLogger.Add(LogSource.LptCtrl, "Session", isCalledByMarker ? "Finished" : "Interrupted");

        _mfc.IsInShortPulseMode = false;
        _mfc.Odor1Speed = MFC.ODOR_MIN_SPEED;
        _mfc.Odor2Speed = MFC.ODOR_MIN_SPEED;
        _mfc.OdorDirection = MFC.ValvesOpened.None;

        _settings = null;
    }

    public void Dispose()
    {
        _runner?.Dispose();
        _pulseController?.Dispose();
        GC.SuppressFinalize(this);

        _comPort?.Dispose();
    }

    // Internal

    const short MARKER_TOBII_FINISHED = 255;
    const byte MARKER_NEXUS_FINISHED = (byte)'#';
    const short LPT_READING_INTERVAL = 5;      // ms
    const double MFC_FLOW_BETWEEN_PULSES = 5;   // maybe, some odor flow must occur also pulses so that it is always ready to be presented

    readonly MFC _mfc = MFC.Instance;
    readonly FlowLogger _eventLogger = FlowLogger.Instance;
    readonly Dispatcher _dispatcher;

    Settings? _settings;

    DispatchOnce? _runner;
    PulsesController? _pulseController;
    ComPort? _comPort;
    LptPort? _lptPort;
    Thread? _lptReadingThread;

    short _marker;

    short[] EmulatedMarkers = Array.Empty<short>();

    private void ReadPortStatus(object? obj)
    {
        try
        {
            while (_lptReadingThread!.ThreadState == ThreadState.Running)
            {
                if (_runner == null)
                {
                    CheckMarker();
                }
                Thread.Sleep(LPT_READING_INTERVAL);
            }
        }
        catch (ThreadInterruptedException) { }

        _lptReadingThread = null;
    }

    private void CheckMarker()
    {
        var marker = _lptPort?.ReadData() ?? LptPort.Emulate(EmulatedMarkers);

        if (marker != _marker)
        {
            _marker = marker;
            _eventLogger.Add(LogSource.LptCtrl, "Marker", _marker.ToString());

            _dispatcher.Invoke(() =>
            {
                if (_marker == MARKER_TOBII_FINISHED)
                {
                    _comPort?.SendMarker(TobiiToNexus(_marker));
                    FinalizeMarker();
                }
                else if (_marker > 0)
                {
                    _comPort?.SendMarker(TobiiToNexus(_marker));
                    UseOdor(_marker);
                }
            });
        }
    }

    private void UseOdor(int id)
    {
        if (_settings!.Pulses.TryGetValue(id, out Pulse? pulse))
        {
            Marker?.Invoke(this, _marker.ToString());

            _eventLogger.Add(LogSource.LptCtrl, "Gas", pulse.ToString());

            CurrentPulse = pulse;
            _mfc.Odor1Speed = pulse.Channel1?.Flow ?? MFC.ODOR_MIN_SPEED;
            _mfc.Odor2Speed = pulse.Channel2?.Flow ?? MFC.ODOR_MIN_SPEED;

            _pulseController = new PulsesController(CurrentPulse, _settings!.OdorFlowDurationMs);
            _pulseController.PulseStateChanged += PulseStateChanged;
            _pulseController.Run();

            _runner = DispatchOnce.Do(_settings.OdorFlowDuration, StopOdorFlow);
        }
        else
        {
            Marker?.Invoke(this, $"{id} [unknown odor]");
        }
    }

    private void StopOdorFlow()
    {
        _eventLogger.Add(LogSource.LptCtrl, "Gas", "Stop");

        if (_mfc.OdorDirection != MFC.ValvesOpened.None)
        {
            _mfc.OdorDirection = MFC.ValvesOpened.None;
        }

        _mfc.Odor1Speed = MFC_FLOW_BETWEEN_PULSES;
        _mfc.Odor2Speed = MFC_FLOW_BETWEEN_PULSES;

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
        });

        Stop(true);

        Finished?.Invoke(this, new EventArgs());
    }

    private void StopProcesses()
    {
        _runner?.Stop();
        _pulseController?.Terminate();
        _lptReadingThread?.Interrupt();
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

        _eventLogger.Add(LogSource.LptCtrl, "Valves", ((int)valves).ToString("D2"));

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
        StopProcesses();
    }

    private void MFC_Closed(object? sender, EventArgs e)
    {
        if (_settings != null)
        {
            StopProcesses();

            MsgBox.Error(
                App.Name + " - " + L10n.T("OdorPulses"),
                string.Format(L10n.T("DeviceConnLost"), "MFC") + " " + L10n.T("AppTerminated"));
            Application.Current.Shutdown();
        }
    }
}
