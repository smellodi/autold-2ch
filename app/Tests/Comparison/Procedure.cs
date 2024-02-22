using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AutOlD2Ch.Comm;
using AutOlD2Ch.Tests.Common;
using AutOlD2Ch.Utils;

namespace AutOlD2Ch.Tests.Comparison;

public class Procedure : ITestEmulator, IDisposable
{
    public enum Answer
    {
        None,
        Same,
        Different
    }

    public enum OutputValveStage
    {
        Closed,
        Opened
    }

    public enum MixtureID
    {
        None = 0,
        First = 1,
        Second = 2
    }

    public class Stage
    {
        public OutputValveStage OutputValveStage { get; private set; }
        public MixtureID MixtureID { get; private set; }

        public Stage(OutputValveStage outputValveStage = OutputValveStage.Closed, MixtureID flowOrder = MixtureID.None)
        {
            OutputValveStage = outputValveStage;
            MixtureID = flowOrder;
            if (OutputValveStage == OutputValveStage.Opened && flowOrder == MixtureID.None)
            {
                throw new Exception("Missing the mixtureID in the valve-opened stage");
            }
        }
    }

    public event EventHandler<double>? Data;
    public event EventHandler<Stage>? StageChanged;
    public event EventHandler? RequestAnswer;
    public event EventHandler<string>? DNSError;
    
    /// <summary>
    /// Fires when a trial is finished. Provides 'true' if there is no more trials to run, 'false' is more trials to run
    /// </summary>
    public event EventHandler<bool>? Finished;

    public List<(MixturePair, Answer)> Results { get; } = new();

    public Procedure()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;

        FlowLogger.Instance.Clear();

        //_dataLogger.Clear();
        _testLogger.Clear();

        _timer.Elapsed += (s, e) =>
        {
            _dispatcher.Invoke(() =>  // we let the timer to count further without waiting for the end of reading from serial ports
            {
                if (_pid.GetSample(out PIDSample pidSample).Error == Error.Success)
                {
                    //_dataLogger.Add(pidSample);
                    _monitor?.LogData(LogSource.PID, pidSample);
                    Data?.Invoke(this, pidSample.PID);
                    CheckPID(pidSample.PID);
                }
                if (_mfc.GetSample(out MFCSample mfcSample).Error == Error.Success)
                {
                    //_dataLogger.Add(mfcSample);
                    _monitor?.LogData(LogSource.MFC, mfcSample);
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
        _step = _pairsOfMixtures.Length - 1;
    }

    public void Start(Settings settings, Comparison.Stage stage)
    {
        _settings = settings;
        _stage = stage;

        // catch window closing event, so we do not display termination message due to MFC comm closed
        Application.Current.MainWindow.Closing += MainWindow_Closing;

        // If the test is in progress, display warning message and quit the app:
        // Or can we recover here by trying to open the COM port again?
        _mfc.Closed += MFC_Closed;
        _pid.Closed += PID_Closed;

        if (_monitor != null)
        {
            _monitor.MFCUpdateInterval = UPDATE_INTERVAL_IN_SECONDS;
            _monitor.PIDUpdateInterval = UPDATE_INTERVAL_IN_SECONDS;
        }

        _mfc.FreshAirSpeed = _settings.FreshAirFlow;
        _mfc.OdorDirection = MFC.ValvesOpened.None; // should I add delay here?

        int intervalInMs = (int)(1000 * UPDATE_INTERVAL_IN_SECONDS);

        _timer.Interval = intervalInMs;
        _timer.AutoReset = true;
        _timer.Start();

        //_dataLogger.Start(intervalInMs);
        _testLogger.Add(LogSource.Comparison, "stage", stage.ToString());
        foreach (var param in _settings.Params)
        {
            _testLogger.Add(LogSource.Comparison, "config", param.Key, param.Value);
        }

        var blockLength = _settings.PairsOfMixtures.Length;
        _pairsOfMixtures = new MixturePair[_settings.Repetitions * blockLength];
        for (int blockID = 0; blockID < _settings.Repetitions; ++blockID)
        {
            for (int i = 0; i < blockLength; ++i)
            {
                _pairsOfMixtures[blockID * blockLength + i] = _settings.PairsOfMixtures[i];
            }

            if (_settings.Sniffer == GasSniffer.Human)
            {
                var random = new Random();
                random.Shuffle(_pairsOfMixtures, blockID * blockLength, blockLength);
            }
        }

        foreach (var pm in _pairsOfMixtures)
        {
            Debug.WriteLine(pm);
        }

        if (_settings.Sniffer == GasSniffer.DMS)
        {
            _dms = DMS.Instance;
        }

        Next();
    }

    public void Next()
    {
        if (_settings == null)
            return;

        var pair = _pairsOfMixtures[_step];

        //_dataLogger.Add(pair.ToString());
        _testLogger.Add(LogSource.Comparison, "trial", "pair", pair.ToString());

        _runner = DispatchOnce
            .Do(0.1, () => StageChanged?.Invoke(this, new Stage(OutputValveStage.Closed, MixtureID.First)))?
            .Then(0.1, () => PrepareOdors(0))
            .Then(_settings.InitialPause, () => StartOdorFlow(0));
        
        if (_settings.WaitForPID)
        {
            _runner?.ThenWait();
        }

        _runner?.Then(_settings.OdorFlowDuration, StopOdorFlow)
            .Then(0.1, () => PrepareOdors(1))
            .Then(_settings.InitialPause, () => StartOdorFlow(1));

        if (_settings.WaitForPID)
        {
            _runner?.ThenWait();
        }

        _runner?.Then(_settings.OdorFlowDuration, () => StopOdorFlow())
            .Then(2.0, () => RequestAnswer?.Invoke(this, new EventArgs()));
    }

    public void SetResult(Answer answer)
    {
        //_dataLogger.Add($"A={answer}");
        _testLogger.Add(LogSource.Comparison, "trial", "answer", answer.ToString());

        var pair = _pairsOfMixtures[_step];
        Results.Add((pair, answer));

        FinalizeTest();
    }

    public void Stop()
    {
        StopTimers();

        _mfc.Odor1Speed = MFC.ODOR_MIN_SPEED;
        _mfc.Odor2Speed = MFC.ODOR_MIN_SPEED;

        if (_mfc.OdorDirection != MFC.ValvesOpened.None)
        {
            _mfc.OdorDirection = MFC.ValvesOpened.None;
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _runner?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    const double UPDATE_INTERVAL_IN_SECONDS = 0.5;
    const double EXPECTED_PID_REDUCTION = 0.85;     // must be < 0.9

    readonly MFC _mfc = MFC.Instance;
    readonly PID _pid = PID.Instance;
    //readonly SyncLogger _dataLogger = SyncLogger.Instance;
    readonly FlowLogger _testLogger = FlowLogger.Instance;
    readonly CommMonitor? _monitor = CommMonitor.Instance;
    readonly System.Timers.Timer _timer = new();
    readonly Dispatcher _dispatcher;

    readonly PulsesController? _pulseController;

    Settings? _settings;
    Comparison.Stage _stage;
    DMS? _dms = null;

    int _step = 0;
    MixturePair[] _pairsOfMixtures = Array.Empty<MixturePair>();
    MixtureID _mixtureID = MixtureID.None;
    double _PIDThreshold = 0;

    DispatchOnce? _runner;
    //DispatchOnce _pulseFinisher;

    private void PrepareOdors(int mixID)
    {
        if (_settings == null)
            return;

        var pair = _pairsOfMixtures[_step];
        var pulse = _stage == Comparison.Stage.Test
            ? GasMixer.ToPulse(pair, mixID, _settings.TestOdorFlow, _settings.OdorFlowDurationMs, _settings.Gas1, _settings.Gas2)
            : GasMixer.ToPulse(pair, mixID, _settings.PracticeOdorFlow, _settings.OdorFlowDurationMs);

        _mfc.Odor1Speed = pulse.Channel1?.Flow ?? MFC.ODOR_MIN_SPEED;
        _mfc.Odor2Speed = pulse.Channel2?.Flow ?? MFC.ODOR_MIN_SPEED;
    }

    private void StartOdorFlow(int mixID)
    {
        if (_settings == null)
            return;

        _mixtureID = NextMixtureID();

        var pair = _pairsOfMixtures[_step];
        var pulse = _stage == Comparison.Stage.Test
            ? GasMixer.ToPulse(pair, mixID, _settings.TestOdorFlow, _settings.OdorFlowDurationMs, _settings.Gas1, _settings.Gas2)
            : GasMixer.ToPulse(pair, mixID, _settings.PracticeOdorFlow, _settings.OdorFlowDurationMs);

        Debug.WriteLine($"[PULSE] {pulse}");
        //_dataLogger.Add("V" + ((int)valves).ToString("D2"));
        /*
        _pulseController = new PulsesController(pulse, _settings.OdorFlowDurationMs);
        _pulseController.PulseStateChanged += PulseStateChanged;*/

        if (_settings.WaitForPID)
        {
            _PIDThreshold = OlfactoryDeviceModel.ComputePID(
                _settings.Gas1, pulse.Channel1?.Flow ?? 0,
                _settings.Gas2, pulse.Channel2?.Flow ?? 0
            ) * EXPECTED_PID_REDUCTION;
            Debug.WriteLine($"[PROC] waiting for the level - {_PIDThreshold}");
        }
        else
        {
            MFC.ValvesOpened valves =
                (pulse.Channel1?.Valve ?? MFC.ValvesOpened.None) |
                (pulse.Channel2?.Valve ?? MFC.ValvesOpened.None);
            _mfc.OdorDirection = valves;

            Task.Delay((int)(_settings.DMSSniffingDelay * 1000)).ContinueWith((t) => _dms?.StartScan(pair, _mixtureID));

            StageChanged?.Invoke(this, new Stage(OutputValveStage.Opened, _mixtureID));
        }
    }

    private void StopOdorFlow()
    {
        var error = _dms?.SaveScan();
        if (error != null)
        {
            var step = _pairsOfMixtures[_step];
            var info = _mixtureID == MixtureID.First ? step.Mix1 : step.Mix2;
            DNSError?.Invoke(this, $"[{_step + 1}.{(int)_mixtureID}] {info}: {error}");
        }

        if (_mfc.OdorDirection != MFC.ValvesOpened.None)
        {
            _mfc.OdorDirection = MFC.ValvesOpened.None;
            //_dataLogger.Add("V" + ((int)MFC.ValvesOpened.None).ToString("D2"));
        }

        StageChanged?.Invoke(this, new Stage(OutputValveStage.Closed, NextMixtureID()));
    }

    private void FinalizeTest()
    {
        _runner?.Dispose();
        _runner = null;
        _mixtureID = MixtureID.None;

        var noMoreTrials = ++_step >= _pairsOfMixtures.Length;
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

    private void CheckPID(double pid)
    {
        if (_settings == null)
            return;

        if (_PIDThreshold != 0 && pid > _PIDThreshold)
        {
            _PIDThreshold = 0;
            _pulseController?.Run();

            var pair = _pairsOfMixtures[_step];
            Task.Delay((int)(_settings.DMSSniffingDelay * 1000)).ContinueWith((t) => _dms?.StartScan(pair, _mixtureID));

            StageChanged?.Invoke(this, new Stage(OutputValveStage.Opened, _mixtureID));

        }
    }

    private MixtureID NextMixtureID()
    {
        return _mixtureID == MixtureID.Second ? MixtureID.None : (MixtureID)((int)_mixtureID + 1);
    }

    // Event handlers
    /*
    private void PulseStateChanged(object sender, PulsesController.PulseStateChangedEventArgs e)
    {
        if (e.IsLast)
        {
            _pulseController = null;
            return;
        }

        if (_settings.WaitForPID)
        {
            _runner.Resume();
        }
        else
        {
            StageChanged?.Invoke(this, new Stage(OutputValveStage.Opened, _mixtureID));
        }
    }*/

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
