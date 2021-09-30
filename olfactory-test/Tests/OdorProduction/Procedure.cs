using System;
using System.Windows;
using System.Windows.Threading;
using Olfactory.Comm;

namespace Olfactory.Tests.OdorProduction
{
    public class Procedure : ITestEmulator
    {
        public enum Stage
        {
            None,
            InitWait,
            OdorFlow,
            FinalWait,
        }

        public int Step => _step;

        public event EventHandler<double> Data;
        public event EventHandler<Stage> StageChanged;
        public event EventHandler<bool> Finished;         // provides 'true' if there is no more trials to run


        public Procedure()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            FlowLogger flowLogger = FlowLogger.Instance;
            flowLogger.Clear();

            _logger.Clear();

            _timer.Elapsed += (s, e) =>
            {
                Dispatcher.CurrentDispatcher.Invoke(() =>  // we let the timer to count further without waiting for the end of reading from serial ports
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

        public void EmulationInit() { }

        public void EmulationFinilize()
        {
            _step = _settings.OdorQuantities.Length - 1;
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

            _initialDirection = _settings.ValvesControlled == MFC.OdorFlowsTo.WasteAndUser ? MFC.OdorFlowsTo.SystemAndWaste : MFC.OdorFlowsTo.Waste;

            _mfc.FreshAirSpeed = _settings.FreshAir;
            _mfc.OdorDirection = _initialDirection; // should I add delay here?

            _timer.Interval = _settings.PIDReadingInterval;
            _timer.AutoReset = true;
            _timer.Start();

            _logger.Start(_settings.PIDReadingInterval);

            Next();
        }

        public void Next()
        {
            var (mlmin, ms) = _settings.OdorQuantities[_step];

            //_logger.Add(LogSource.OdProd, "trial", "start", _settings.OdorQuantities[_step].ToString());
            _logger.Add($"S{mlmin}" + (ms > 0 ? $"{ms}" : ""));

            _runner = Utils.DispatchOnce
                .Do(0.1, () =>
                {
                    _mfc.OdorSpeed = mlmin;
                    StageChanged?.Invoke(this, Stage.InitWait);
                })
                .Then(_settings.InitialPause > 0 ? _settings.InitialPause : 0.1, () => StartOdorFlow())
                .Then(_settings.OdorFlowDuration, () => StopOdorFlow())
                .Then(_settings.FinalPause > 0 ? _settings.FinalPause : 0.1, () => Finilize());
        }

        public void Stop()
        {
            _runner?.Stop();
            _timer.Stop();

            _mfc.IsInShortPulseMode = false;
            _mfc.OdorSpeed = MFC.ODOR_MIN_SPEED;
            _mfc.OdorDirection = MFC.OdorFlowsTo.Waste;
        }


        // Internal

        readonly MFC _mfc = MFC.Instance;
        readonly PID _pid = PID.Instance;
        readonly SyncLogger _logger = SyncLogger.Instance;
        readonly CommMonitor _monitor = CommMonitor.Instance;
        readonly System.Timers.Timer _timer = new();
        readonly Dispatcher _dispatcher;

        Settings _settings;

        MFC.OdorFlowsTo _initialDirection;
        int _step = 0;

        Utils.DispatchOnce _runner;


        private void StartOdorFlow()
        {
            _logger.Add("V" + ((int)_settings.ValvesControlled).ToString("D2"));

            var direction = _settings.ValvesControlled | MFC.OdorFlowsTo.System;
            var (mlmin, ms) = _settings.OdorQuantities[_step];
            var duration = ms == 0 ? _settings.OdorFlowDuration : (double)ms / 1000;
            var useShortPulse = _settings.UseValveTimer
                && 0 < duration && duration <= MFC.MAX_SHORT_PULSE_DURATION
                && direction.HasFlag(MFC.OdorFlowsTo.User);

            if (useShortPulse)
            {
                _mfc.PrepareForShortPulse(duration);
            }
            else
            {
                _mfc.IsInShortPulseMode = false;
            }

            _mfc.OdorDirection = direction;

            if (_settings.UseFeedbackLoopToReachLevel)
            {
                var model = new OlfactoryDeviceModel();
                model.Reach(mlmin, duration, _settings.UseFeedbackLoopToKeepLevel);
            }

            StageChanged?.Invoke(this, Stage.OdorFlow);
        }

        private void StopOdorFlow()
        {
            _mfc.OdorDirection = _initialDirection;

            _logger.Add("V00");

            StageChanged?.Invoke(this, Stage.FinalWait);
        }

        private void Finilize()
        {
            _runner = null;

            var noMoreTrials = ++_step >= _settings.OdorQuantities.Length;
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
                _timer.Stop();
                _runner?.Stop();

                Utils.MsgBox.Error(
                    Utils.L10n.T("OlfactoryTestTool") + " - " + Utils.L10n.T("OdorPulses"),
                    string.Format(Utils.L10n.T("DeviceConnLost"), source) + " " + Utils.L10n.T("AppTerminated"));
                Application.Current.Shutdown();
            }
        }

        // Event handlers

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            _runner?.Stop();
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
}
