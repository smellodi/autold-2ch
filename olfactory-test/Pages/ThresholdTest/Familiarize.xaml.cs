using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Familiarize : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next = delegate { };

        MFC _mfc = MFC.Instance;

        DispatcherTimer _countdownTimer = new DispatcherTimer();
        DispatcherTimer _directionChangeTimer = new DispatcherTimer();
        int _waitingCountdown = 0;

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

        private void OnNext_Click(object sender, RoutedEventArgs e)
        {
            _mfc.OdorSpeed = 1.0;

            Utils.DispatchOnce.Do(0.5, () => _mfc.OdorDirection = MFC.OdorFlow.ToWaste);    // just in case, make 0.5 sec delay between the requests

            Next(this, new EventArgs());
        }
    }
}
