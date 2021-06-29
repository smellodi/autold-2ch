using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MFC = Olfactory.Comm.MFC;

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

            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += (s, e) =>
            {
                if (--_waitingCountdown > 0)
                {
                    txbCountdown.Text = $"{_waitingCountdown} seconds left";
                }
                else
                {
                    txbCountdown.Text = "";
                    _countdownTimer.Stop();
                }
            };

            _stateTimer.Tick += (s, e) =>
            {
                _stateTimer.Stop();

                if (_state == State.OdorPreparation)
                {
                    _state = State.OdorFlow;
                    txbInstruction.Text = "Odor is flowing now, sniff it!";

                    _sniffingStartTimestamp = Utils.Timestamp.Ms;

                    _stateTimer.Interval = TimeSpan.FromSeconds(_settings.FamiliarizationDuration);
                    _stateTimer.Start();
                }
                else if (_state == State.OdorFlow)
                {
                    _state = State.Ventilation;
                    txbInstruction.Text = "Please wait while the tube is ventilating...";

                    _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndWaste;
                    Utils.DispatchOnce.Do(0.3, () => _mfc.OdorSpeed = 1.0);    // just in case, make 0.3 sec delay between the requests

                    _waitingCountdown = VENTILATION_DURATION;
                    _countdownTimer.Start();

                    _stateTimer.Interval = TimeSpan.FromSeconds(VENTILATION_DURATION);
                    _stateTimer.Start();
                }
                else if (_state == State.Ventilation)
                {
                    _state = State.Finished;
                    txbInstruction.Text = "Click 'Continue' to start the test.";

                    btnNext.IsEnabled = true;
                }
            };

            _directionChangeTimer.Tick += (s, e) => {
                _directionChangeTimer.Stop();
                _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndUser;
            };
        }

        public void Init(Tests.ThresholdTest.Settings settings)
        {
            _settings = settings;
            _mfc.FreshAirSpeed = _settings.FreshAir;
        }

        public void Interrupt()
        {
            _stateTimer.Stop();
            _directionChangeTimer.Stop();
            _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndWaste;
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

        MFC _mfc = MFC.Instance;

        DispatcherTimer _stateTimer = new DispatcherTimer();
        DispatcherTimer _countdownTimer = new DispatcherTimer();
        DispatcherTimer _directionChangeTimer = new DispatcherTimer();

        int _waitingCountdown = 0;
        long _sniffingStartTimestamp = 0;

        State _state = State.Initial;

        Tests.ThresholdTest.Settings _settings;

        // UI events

        private void btnOpenValve_Click(object sender, RoutedEventArgs e)
        {
            _mfc.OdorSpeed = MFC.ODOR_MAX_SPEED;

            _state = State.OdorPreparation;
            txbInstruction.Text = "Odor is soon to reach you...";
            txbCountdown.Text = $"{_waitingCountdown} seconds left";

            _directionChangeTimer.Interval = TimeSpan.FromSeconds(_mfc.EstimateFlowDuration(MFC.FlowStartPoint.Chamber, MFC.FlowEndPoint.Mixer));
            _directionChangeTimer.Start();

            _waitingCountdown = (int)Math.Ceiling(_mfc.EstimateFlowDuration(MFC.FlowStartPoint.Chamber, MFC.FlowEndPoint.User));
            _countdownTimer.Start();

            _stateTimer.Interval = TimeSpan.FromSeconds(_waitingCountdown);
            _stateTimer.Start();

            (sender as Button).IsEnabled = false;
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
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
