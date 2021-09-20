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

        public event EventHandler<double> Data = delegate { };
        public event EventHandler<Stage> StageChanged = delegate { };
        public event EventHandler<bool> Finished = delegate { };         // provides 'true' if there is no more trials to run


        public Procedure()
        {
            // catch window closing event, so we do not display termination message due to MFC comm closed
            Application.Current.MainWindow.Closing += (s, e) =>
            {
                _timer.Stop();
            };

            // If the test is in progress, display warning message and quit the app:
            // Or can we recover here by trying to open the COM port again?
            _mfc.Closed += (s, e) =>
            {
                if (_timer.Enabled)
                {
                    _timer.Stop();
                    _runner?.Stop();

                    Utils.MsgBox.Error(
                        Utils.L10n.T("OlfactoryTestTool") + " - " + Utils.L10n.T("OdorPulses"),
                        string.Format(Utils.L10n.T("DeviceConnLost"), "MFC") + " " + Utils.L10n.T("AppTerminated"));
                    Application.Current.Shutdown();
                }
            };
            _pid.Closed += (s, e) =>
            {
                if (_timer.Enabled)
                {
                    _timer.Stop();
                    _runner?.Stop();

                    Utils.MsgBox.Error(
                        Utils.L10n.T("OlfactoryTestTool") + " - " + Utils.L10n.T("OdorPulses"),
                        string.Format(Utils.L10n.T("DeviceConnLost"), "PID") + " " + Utils.L10n.T("AppTerminated"));
                    Application.Current.Shutdown();
                }
            };

            _timer.Elapsed += (s, e) =>
            {
                Dispatcher.CurrentDispatcher.Invoke(() =>  // we let the timer to count further without waiting for the end of reading from serial ports
                {
                    if (_pid.GetSample(out PIDSample pidSample).Error == Error.Success)
                    {
                        _logger.Add(pidSample);
                        _monitor.LogData(LogSource.PID, pidSample);
                        Data(this, pidSample.PID);
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

            _mfc.FreshAirSpeed = _settings.FreshAir;
            _mfc.OdorDirection = MFC.OdorFlowsTo.Waste; // should I add delay here?

            _timer.Interval = _settings.PIDReadingInterval;
            _timer.AutoReset = true;
            _timer.Start();

            _logger.Start(_settings.PIDReadingInterval);

            Next();
        }

        public void Next()
        {
            //_logger.Add(LogSource.OdProd, "trial", "start", _settings.OdorQuantities[_step].ToString());
            _logger.Add("S" + _settings.OdorQuantities[_step].ToString());

            _runner = Utils.DispatchOnce
                .Do(0.1, () =>
                {
                    _mfc.OdorSpeed = _settings.OdorQuantities[_step];
                    StageChanged(this, Stage.InitWait);
                })
                .Then(_settings.InitialPause > 0 ? _settings.InitialPause : 0.1, () => StartOdorFlow())
                .Then(_settings.OdorFlowDuration, () => StopOdorFlow())
                .Then(_settings.FinalPause > 0 ? _settings.FinalPause : 0.1, () => Finilize());
        }

        public void Interrupt()
        {
            _timer.Stop();
            _runner?.Stop();

            _mfc.OdorSpeed = MFC.ODOR_MIN_SPEED;
            _mfc.OdorDirection = MFC.OdorFlowsTo.Waste;
        }


        // Internal

        Settings _settings;

        int _step = 0;

        MFC _mfc = MFC.Instance;
        PID _pid = PID.Instance;
        SyncLogger _logger = SyncLogger.Instance;
        CommMonitor _monitor = CommMonitor.Instance;

        System.Timers.Timer _timer = new System.Timers.Timer();
        Utils.DispatchOnce _runner;


        private void StartOdorFlow()
        {
            _logger.Add("V" + (_settings.Valve2ToUser ? "11" : "10"));

            var direction = _settings.Valve2ToUser ? MFC.OdorFlowsTo.SystemAndUser : MFC.OdorFlowsTo.SystemAndWaste;

            if (_settings.OdorFlowDuration < 1)
            {
                _mfc.ShortPulse(_settings.OdorFlowDuration, direction);
            }
            else
            {
                _mfc.OdorDirection = direction;

                if (_settings.UseFeedbackLoopToReachLevel)
                {
                    var model = new OlfactoryDeviceModel();
                    model.Reach(_settings.OdorQuantities[_step], _settings.OdorFlowDuration, _settings.UseFeedbackLoopToKeepLevel);
                }
            }

            StageChanged(this, Stage.OdorFlow);
        }

        private void StopOdorFlow()
        {
            if (_settings.OdorFlowDuration >= 1)
            {
                _mfc.OdorDirection = MFC.OdorFlowsTo.Waste;
            }

            _logger.Add("V00");

            StageChanged(this, Stage.FinalWait);
        }

        private void Finilize()
        {
            //_logger.Add(LogSource.OdProd, "trial", "finished");

            var noMoreTrials = ++_step >= _settings.OdorQuantities.Length;
            if (noMoreTrials)
            {
                _mfc.OdorSpeed = MFC.ODOR_MIN_SPEED;
                _timer.Stop();
            }

            Finished(this, noMoreTrials);
        }
    }
}
