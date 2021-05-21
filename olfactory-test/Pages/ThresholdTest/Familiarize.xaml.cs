using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Familiarize : Page, IPage<long>
    {
        public event EventHandler<long> Next = delegate { };    // passes duration of sniffing in milliseconds

        MFC _mfc = MFC.Instance;

        DispatcherTimer _countdownTimer = new DispatcherTimer();
        DispatcherTimer _directionChangeTimer = new DispatcherTimer();
        int _waitingCountdown = 0;
        long _sniffingStartTimestamp;

        public Familiarize()
        {
            InitializeComponent();

            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += (s, e) =>
            {
                if (--_waitingCountdown > 0)
                {
                    txbCountdown.Text = $"{_waitingCountdown} seconds left";
                }
                else
                {
                    txbCountdown.Text = $"Odor is flowing now. Click 'Continue' when it is enough.";
                    btnNext.IsEnabled = true;
                    _countdownTimer.Stop();

                    _sniffingStartTimestamp = Utils.Timestamp.Value;
                }
            };

            _directionChangeTimer.Tick += (s, e) => {
                _directionChangeTimer.Stop();
                _mfc.OdorDirection = MFC.OdorFlow.ToUser;
            };
        }


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
            _mfc.OdorDirection = MFC.OdorFlow.ToWaste;

            // We do not need this command as the next page immediately sets the odor speed to some other value
            //Utils.DispatchOnce.Do(0.5, () => _mfc.OdorSpeed = 1.0);    // just in case, make 0.5 sec delay between the requests

            Next(this, Utils.Timestamp.Value - _sniffingStartTimestamp);
        }
    }
}
