﻿using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Olfactory.Comm;
using Olfactory.Tests.Common;
using Olfactory.Utils;

namespace Olfactory.Tests.Comparison
{
    public class Procedure : ITestEmulator
    {
        public enum Answer
        {
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

        public event EventHandler<double> Data;
        public event EventHandler<Stage> StageChanged;
        public event EventHandler RequestAnswer;
        /// <summary>
        /// Fires when a trial is finished. Provides 'true' if there is no more trials to run, 'false' is more trials to run
        /// </summary>
        public event EventHandler<bool> Finished;


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
                        _monitor.LogData(LogSource.PID, pidSample);
                        Data?.Invoke(this, pidSample.PID);
                        CheckPID(pidSample.PID);
                    }
                    if (_mfc.GetSample(out MFCSample mfcSample).Error == Error.Success)
                    {
                        //_dataLogger.Add(mfcSample);
                        _monitor.LogData(LogSource.MFC, mfcSample);
                    }
                });
            };
        }

        public void EmulationInit()
        {
            // nothing is needed here
        }

        public void EmulationFinilize()
        {
            _step = _settings.PairsOfMixtures.Length - 1;
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

            _monitor.MFCUpdateInterval = UPDATE_INTERVAL_IN_SECONDS;
            _monitor.PIDUpdateInterval = UPDATE_INTERVAL_IN_SECONDS;

            _mfc.FreshAirSpeed = _settings.FreshAirFlow;
            _mfc.OdorDirection = MFC.ValvesOpened.None; // should I add delay here?

            int intervalInMs = (int)(1000 * UPDATE_INTERVAL_IN_SECONDS);

            _timer.Interval = intervalInMs;
            _timer.AutoReset = true;
            _timer.Start();

            //_dataLogger.Start(intervalInMs);
            foreach (var param in _settings.Params)
            {
                _testLogger.Add(LogSource.Comparison, "config", param.Key, param.Value);
            }

            Next();
        }

        public void Next()
        {
            var pair = _settings.PairsOfMixtures[_step];

            //_dataLogger.Add(pair.ToString());
            _testLogger.Add(LogSource.Comparison, "trial", "pair", pair.ToString());

            _runner = DispatchOnce
                .Do(0.1, () => StageChanged?.Invoke(this, new Stage(OutputValveStage.Closed, MixtureID.First)))
                .Then(0.1, () => PrepareOdors(0))
                .Then(_settings.InitialPause, () => StartOdorFlow(0));
            
            if (_settings.WaitForPID)
            {
                _runner.ThenWait();
            }

            _runner.Then(_settings.OdorFlowDuration, () => StopOdorFlow())
                .Then(0.1, () => PrepareOdors(1))
                .Then(_settings.InitialPause, () => StartOdorFlow(1));

            if (_settings.WaitForPID)
            {
                _runner.ThenWait();
            }

            _runner.Then(_settings.OdorFlowDuration, () => StopOdorFlow())
                .Then(2.0, () => RequestAnswer?.Invoke(this, new EventArgs()));
        }

        public void SetResult(Answer answer)
        {
            //_dataLogger.Add($"A={answer}");
            _testLogger.Add(LogSource.Comparison, "trial", "answer", answer.ToString());

            Finilize();
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

        // Internal

        const double UPDATE_INTERVAL_IN_SECONDS = 0.5;
        const double EXPECTED_PID_REDUCTION = 0.85;     // must be < 0.9

        readonly MFC _mfc = MFC.Instance;
        readonly PID _pid = PID.Instance;
        //readonly SyncLogger _dataLogger = SyncLogger.Instance;
        readonly FlowLogger _testLogger = FlowLogger.Instance;
        readonly CommMonitor _monitor = CommMonitor.Instance;
        readonly System.Timers.Timer _timer = new();
        readonly Dispatcher _dispatcher;

        Settings _settings;

        int _step = 0;
        MixtureID _mixtureID = MixtureID.None;
        double _PIDThreshold = 0;

        DispatchOnce _runner;
        PulsesController _pulseController;
        DispatchOnce _pulseFinisher;

        private void PrepareOdors(int mixID)
        {
            var pair = _settings.PairsOfMixtures[_step];
            var pulse = GasMixer.ToPulse(pair, mixID, _settings.OdorFlow, _settings.OdorFlowDurationMs, _settings.Gas1, _settings.Gas2);

            _mfc.Odor1Speed = pulse.Channel1?.Flow ?? MFC.ODOR_MIN_SPEED;
            _mfc.Odor2Speed = pulse.Channel2?.Flow ?? MFC.ODOR_MIN_SPEED;
        }

        private void StartOdorFlow(int mixID)
        {
            _mixtureID = NextMixtureID();

            var pair = _settings.PairsOfMixtures[_step];
            var pulse = GasMixer.ToPulse(pair, mixID, _settings.OdorFlow, _settings.OdorFlowDurationMs, _settings.Gas1, _settings.Gas2);

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
                Debug.WriteLine(_PIDThreshold);
            }
            else
            {
                MFC.ValvesOpened valves =
                    (pulse.Channel1?.Valve ?? MFC.ValvesOpened.None) |
                    (pulse.Channel2?.Valve ?? MFC.ValvesOpened.None);
                _mfc.OdorDirection = valves;

                StageChanged?.Invoke(this, new Stage(OutputValveStage.Opened, _mixtureID));
            }
        }

        private void StopOdorFlow()
        {
            if (_mfc.OdorDirection != MFC.ValvesOpened.None)
            {
                _mfc.OdorDirection = MFC.ValvesOpened.None;
                //_dataLogger.Add("V" + ((int)MFC.ValvesOpened.None).ToString("D2"));
            }
            StageChanged?.Invoke(this, new Stage(OutputValveStage.Closed, NextMixtureID()));
        }

        private void Finilize()
        {
            _runner = null;
            _mixtureID = MixtureID.None;

            var noMoreTrials = ++_step >= _settings.PairsOfMixtures.Length;
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
            _pulseFinisher?.Stop();
        }

        private void CheckPID(double pid)
        {
            if (_PIDThreshold != 0)
            {
                if (pid > _PIDThreshold)
                {
                    _PIDThreshold = 0;
                    _pulseController.Run();
                    StageChanged?.Invoke(this, new Stage(OutputValveStage.Opened, _mixtureID));
                }
            }
        }

        private MixtureID NextMixtureID()
        {
            return _mixtureID == MixtureID.Second ? MixtureID.None : (MixtureID)((int)_mixtureID + 1);
        }

        // Event handlers

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
}
