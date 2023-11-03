using System;
using System.Windows;
using System.Windows.Threading;
using AutOlD2Ch.Comm;
using AutOlD2Ch.Tests.Common;
using AutOlD2Ch.Utils;

namespace AutOlD2Ch.Tests.OdorProduction;

public class Procedure : ITestEmulator, IDisposable
{
    [Flags]
    public enum Stage
    {
        None = 0,
        InitWait = 1,
        BeforeFinalWait = 2,
        FinalWait = 4,
        Odor1Flow = 8,
        Odor2Flow = 16,
        OdorFlow = 32,
    }

    public int Step => _step;

    public event EventHandler<double> Data;
    public event EventHandler<Stage> StageChanged;
    /// <summary>
    /// Fires when a trial is finished. Provides 'true' if there is no more trials to run, 'false' is more trials to run
    /// </summary>
    public event EventHandler<bool> Finished;


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

    public void EmulationInit()
    {
        // nothing is needed here
    }

    public void EmulationFinalize()
    {
        _step = _pulses.Length - 1;
    }

    public void Start(Settings settings)
    {
        _settings = settings;

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
        _mfc.OdorDirection = MFC.ValvesOpened.None; // should I add delay here?
        _mfc.IsInShortPulseMode = _settings.UseValveTimer;

        _timer.Interval = _settings.PIDReadingInterval;
        _timer.AutoReset = true;
        _timer.Start();

        _logger.Start(_settings.PIDReadingInterval);

        _pulses = _settings.Pulses;
        if (_settings.RandomizeOrder)
        {
            new Random().Shuffle(_pulses);
        }

        Next();
    }

    public void Next()
    {
        var pulse = _pulses[_step];

        _logger.Add($"S{pulse.Channel1?.Flow ?? 0}/{pulse.Channel2?.Flow ?? 0}");

        _runner = DispatchOnce
            .Do(0.1, () =>
            {
                _mfc.Odor1Speed = pulse.Channel1?.Flow ?? MFC.ODOR_MIN_SPEED;
                _mfc.Odor2Speed = pulse.Channel2?.Flow ?? MFC.ODOR_MIN_SPEED;
                StageChanged?.Invoke(this, Stage.InitWait);
            })
            .Then(_settings.InitialPause > 0 ? _settings.InitialPause : 0.1, StartOdorFlow)
            .Then(_settings.OdorFlowDuration, StopOdorFlow)
            .Then(_settings.FinalPause > 0 ? _settings.FinalPause : 0.1, FinalizeTest);
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
    }

    // Internal

    readonly MFC _mfc = MFC.Instance;
    readonly PID _pid = PID.Instance;
    readonly SyncLogger _logger = SyncLogger.Instance;
    readonly CommMonitor _monitor = CommMonitor.Instance;
    readonly System.Timers.Timer _timer = new();
    readonly Dispatcher _dispatcher;

    Settings _settings;

    int _step = 0;
    Pulse[] _pulses = null;

    DispatchOnce _runner;
    PulsesController _pulseController;
    //DispatchOnce _pulseFinisher;

    private void StartOdorFlow()
    {
        var pulse = _pulses[_step];
        _pulseController = new PulsesController(pulse, _settings.OdorFlowDurationMs);
        _pulseController.PulseStateChanged += PulseStateChanged;
        _pulseController.Run();
    }

    private void StopOdorFlow()
    {
        if (_settings.ManualFlowStop)
        {
            _logger.Add("M");
            MsgBox.Notify(App.Name, L10n.T("ManualFlowStopMsg"), new string[] { L10n.T("Close") });
        }

        CloseValves();
        StageChanged?.Invoke(this, Stage.FinalWait);
    }

    private void FinalizeTest()
    {
        _runner?.Dispose();
        _runner = null;

        var noMoreTrials = ++_step >= _pulses.Length;
        if (noMoreTrials)
        {
            Stop();
        }

        _dispatcher.Invoke(() =>
        {
            Application.Current.MainWindow.Closing -= MainWindow_Closing;
            _mfc.Closed -= MFC_Closed;
            _pid.Closed -= PID_Closed;
        });

        Finished?.Invoke(this, noMoreTrials);
    }

    private void CloseValves()
    {
        if (_mfc.OdorDirection != MFC.ValvesOpened.None)
        {
            _mfc.OdorDirection = MFC.ValvesOpened.None;
            _logger.Add("V" + ((int)MFC.ValvesOpened.None).ToString("D2"));
        }
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
        //_pulseFinisher?.Stop();
    }

    // Event handlers

    private void PulseStateChanged(object sender, PulsesController.PulseStateChangedEventArgs e)
    {
        if (e.IsLast)
        {
            _pulseController = null;

            if (_mfc.OdorDirection != MFC.ValvesOpened.None && !_settings.ManualFlowStop)
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

        if (_settings.UseValveTimer)
        {
            foreach (var startingChannel in e.StartingChannels)
            {
                _mfc.StartPulse(
                    startingChannel.Valve,
                    startingChannel.GetDuration(_settings.OdorFlowDurationMs));
            }
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
