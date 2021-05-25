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
            InitWait,
            OdorFlow,
            FinalWait,
        }

        public int Step => _step;

        public event EventHandler<Stage> StageChanged = delegate { };
        public event EventHandler<bool> Finished = delegate { };         // provides 'true' if there is no more trials to run


        public Procedure()
        {
            // catch window closing event, so we do not display termination message due to MFC comm closed
            Application.Current.MainWindow.Closing += (s, e) =>
            {
                _pidTimer.Stop();
            };

            // If the test is in progress, display warning message and quit the app:
            // Or can we recover here by trying to open the COM port again?
            _mfc.Closed += (s, e) =>
            {
                if (_pidTimer.IsEnabled)
                {
                    MessageBox.Show("Connection with the MFC device was shut down. The application is terminated.");
                    Application.Current.Shutdown();
                }
            };
            _pid.Closed += (s, e) =>
            {
                if (_pidTimer.IsEnabled)
                {
                    _pidTimer.Stop();
                    _runner?.Stop();

                    MessageBox.Show("Connection with the PID device was shut down. The application is terminated.");
                    Application.Current.Shutdown();
                    //Finished(this, true);
                }
            };

            _pidTimer.Tick += (s, e) =>
            {
                if (_pid.GetSample(out Comm.PIDSample sample).Error == Comm.Error.Success)
                {
                    _logger.Add(LogSource.PID, "data", sample.ToString());
                    CommMonitor.Instance.LogData(LogSource.PID, sample);
                }
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
            _mfc.OdorDirection = MFC.OdorFlow.ToWaste; // should I add delay here?

            _pidTimer.Interval = TimeSpan.FromMilliseconds(_settings.PIDReadingInterval);
            _pidTimer.Start();

            Next();
        }

        public void Next()
        {
            _logger.Add(LogSource.OdProd, "trial", "start", _settings.OdorQuantities[_step].ToString());

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


        // Internal

        Settings _settings;

        int _step = 0;

        MFC _mfc = MFC.Instance;
        PID _pid = PID.Instance;
        Logger _logger = Logger.Instance;

        DispatcherTimer _pidTimer = new DispatcherTimer();
        Utils.DispatchOnce _runner;


        private void StartOdorFlow()
        {
            _mfc.OdorDirection = MFC.OdorFlow.ToUser;
            _logger.Add(LogSource.OdProd, "valve", "open");

            StageChanged(this, Stage.OdorFlow);
        }

        private void StopOdorFlow()
        {
            _mfc.OdorDirection = MFC.OdorFlow.ToWaste;
            _logger.Add(LogSource.OdProd, "valve", "close");

            StageChanged(this, Stage.FinalWait);
        }

        private void Finilize()
        {
            _logger.Add(LogSource.OdProd, "trial", "finished");

            var noMoreTrials = ++_step >= _settings.OdorQuantities.Length;
            if (noMoreTrials)
            {
                _mfc.OdorSpeed = MFC.ODOR_MIN_SPEED;
                _mfc.OdorDirection = MFC.OdorFlow.ToWaste;
                _pidTimer.Stop();
            }

            Finished(this, noMoreTrials);
        }
    }
}
