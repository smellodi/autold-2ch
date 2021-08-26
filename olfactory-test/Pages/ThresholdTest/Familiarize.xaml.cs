using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Olfactory.Comm;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Familiarize : Page, IPage<long>
    {
        public event EventHandler<long> Next = delegate { };    // passes duration of sniffing in milliseconds

        public Familiarize()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            _stateTimer.Elapsed += (s, e) => Dispatcher.Invoke(NextState);

            _directionChangeTimer.Elapsed += (s, e) => Dispatcher.Invoke(OpenValve);

            _measurementTimer.Interval = 1000;
            _measurementTimer.Elapsed += (s, e) => Dispatcher.Invoke(Measure);

            wtiInstruction.Text = INSTRUCTION_OPEN_VALVE;
        }

        public void Init(Tests.ThresholdTest.Settings settings)
        {
            _settings = settings;
            _mfc.FreshAirSpeed = _settings.FreshAir;

            _measurementTimer.Start();
        }

        public void Interrupt()
        {
            _stateTimer.Stop();
            _directionChangeTimer.Stop();
            _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndWaste;

            _measurementTimer.Stop();
        }

        // Internal

        enum State
        {
            Initial,
            OdorPreparation,
            OdorFlow,
            Ventilation,
            Finished
        }

        const int VENTILATION_DURATION = 15;  // seconds

        const string INSTRUCTION_OPEN_VALVE = "After opening the valve, please wait for few seconds before the odour reaches you, then spend some time memorizing it.";
        const string INSTRUCTION_WAIT_UNTIL_READY = "Preparing the odour";
        const string INSTRUCTION_SNIFF = "Odour is flowing now, sniff it!";
        const string INSTRUCTION_WAIT_UNTIL_VENTILATED = "Please wait while the tube is ventilating...";
        const string INSTRUCTION_CONTINUE = "Click 'Continue' to start the test.";

        MFC _mfc = MFC.Instance;
        PID _pid = PID.Instance;
        CommMonitor _monitor = CommMonitor.Instance;

        System.Timers.Timer _stateTimer = new System.Timers.Timer();
        System.Timers.Timer _directionChangeTimer = new System.Timers.Timer();
        System.Timers.Timer _measurementTimer = new System.Timers.Timer();

        long _sniffingStartTimestamp = 0;

        State _state = State.Initial;

        Tests.ThresholdTest.Settings _settings;

        private void NextState()
        {
            _stateTimer.Stop();

            if (_state == State.OdorPreparation)
            {
                _state = State.OdorFlow;

                wtiInstruction.Reset();
                wtiInstruction.Text = INSTRUCTION_SNIFF;

                _sniffingStartTimestamp = Utils.Timestamp.Ms;

                _stateTimer.Interval = 1000 * _settings.FamiliarizationDuration;
                _stateTimer.Start();
            }
            else if (_state == State.OdorFlow)
            {
                _state = State.Ventilation;

                wtiInstruction.Text = INSTRUCTION_WAIT_UNTIL_VENTILATED;

                _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndWaste;
                Utils.DispatchOnce.Do(0.3, () => _mfc.OdorSpeed = 1.0);    // just in case, make 0.3 sec delay between the requests

                wtiInstruction.Start(VENTILATION_DURATION);

                _stateTimer.Interval = 1000 * VENTILATION_DURATION;
                _stateTimer.Start();
            }
            else if (_state == State.Ventilation)
            {
                _state = State.Finished;

                wtiInstruction.Reset();
                wtiInstruction.Text = INSTRUCTION_CONTINUE;

                btnNext.IsEnabled = true;
            }
        }

        private void OpenValve()
        {
            _directionChangeTimer.Stop();
            _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndUser;
        }

        private void Measure()
        {
            if (_pid.GetSample(out PIDSample pidSample).Error == Error.Success)
            {
                _monitor.LogData(LogSource.PID, pidSample);
            }
            if (_mfc.GetSample(out MFCSample mfcSample).Error == Error.Success)
            {
                _monitor.LogData(LogSource.MFC, mfcSample);
            }
        }

    // UI events

    private void btnOpenValve_Click(object sender, RoutedEventArgs e)
        {
            _mfc.OdorSpeed = MFC.ODOR_MAX_SPEED;

            _state = State.OdorPreparation;

            _directionChangeTimer.Interval = 1000 * _mfc.EstimateFlowDuration(MFC.FlowStartPoint.Chamber, MFC.FlowEndPoint.Mixer);
            _directionChangeTimer.Start();

            var waitingInterval = (double)Math.Ceiling(_mfc.EstimateFlowDuration(MFC.FlowStartPoint.Chamber, MFC.FlowEndPoint.User));
            wtiInstruction.Text = INSTRUCTION_WAIT_UNTIL_READY;
            wtiInstruction.Start(waitingInterval);

            _stateTimer.Interval = 1000 * waitingInterval;
            _stateTimer.Start();

            (sender as Button).IsEnabled = false;
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            _measurementTimer.Stop();

            if (_settings.FamiliarizationDuration > 0)
            {
                Next(this, (long)(_settings.FamiliarizationDuration * 1000));
            }
            else
            {
                btnNext.IsEnabled = false;

                _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndWaste;
                Utils.DispatchOnce.Do(0.3, () => _mfc.OdorSpeed = 1.0);    // just in case, make 0.3 sec delay between the requests
                Utils.DispatchOnce.Do(1, () => Next(this, Utils.Timestamp.Ms - _sniffingStartTimestamp));
            }
        }
    }
}
