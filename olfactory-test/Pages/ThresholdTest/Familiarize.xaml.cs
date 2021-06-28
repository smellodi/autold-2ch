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
                else if (_sniffingStartTimestamp == 0)
                {
                    _countdownTimer.Stop();

                    _sniffingStartTimestamp = Utils.Timestamp.Ms;

                    if (_settings.FamiliarizationDuration > 0)
                    {
                        txbCountdown.Text = $"Odor is flowing now, sniff it!";
                        _countdownTimer.Interval = TimeSpan.FromSeconds(_settings.FamiliarizationDuration);
                        _countdownTimer.Start();
                    }
                    else
                    {
                        txbCountdown.Text = $"Odor is flowing now, sniff it! Click 'Continue' when it is enough.";
                        btnNext.IsEnabled = true;
                    }
                }
                else
                {
                    _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndWaste;
                    Utils.DispatchOnce.Do(0.3, () => _mfc.OdorSpeed = 1.0);    // just in case, make 0.3 sec delay between the requests

                    txbCountdown.Text = $"Click 'Continue' to start the test.";
                    _countdownTimer.Stop();
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
            _countdownTimer.Stop();
            _directionChangeTimer.Stop();
            _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndWaste;
        }

        // Internal

        MFC _mfc = MFC.Instance;

        DispatcherTimer _countdownTimer = new DispatcherTimer();
        DispatcherTimer _directionChangeTimer = new DispatcherTimer();
        int _waitingCountdown = 0;
        long _sniffingStartTimestamp = 0;

        Tests.ThresholdTest.Settings _settings;

        // UI events

        private void btnOpenValve_Click(object sender, RoutedEventArgs e)
        {
            _mfc.OdorSpeed = MFC.ODOR_MAX_SPEED;

            _directionChangeTimer.Interval = TimeSpan.FromSeconds(_mfc.EstimateFlowDuration(MFC.FlowEndPoint.Mixer));
            _waitingCountdown = (int)Math.Ceiling(_mfc.EstimateFlowDuration(MFC.FlowEndPoint.User));

            _directionChangeTimer.Start();
            _countdownTimer.Start();

            txbCountdown.Text = $"{_waitingCountdown} seconds left";

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
