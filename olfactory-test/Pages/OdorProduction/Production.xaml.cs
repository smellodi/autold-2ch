using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Olfactory.Tests.OdorProduction;
using MFC = Olfactory.Comm.MFC;

namespace Olfactory.Pages.OdorProduction
{
    public partial class Production : Page, IPage<int>
    {
        public event EventHandler<int> Next = delegate { };    // passes the step ID that is awaited next to run
        public event EventHandler Finished = delegate { };

        public Production()
        {
            InitializeComponent();

            var intervalStyle = FindResource("Interval") as Style;

            _inactiveIntervalStyle = new Style(typeof(StackPanel), intervalStyle);

            _activeIntervalStyle = new Style(typeof(StackPanel), intervalStyle);
            _activeIntervalStyle.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(102, 205, 255))));
            _activeIntervalStyle.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(Color.FromRgb(255, 255, 255))));

            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += (s, e) =>
            {
                if (--_waitingCountdown > 0)
                {
                    lblCountdown.Content = _waitingCountdown;
                }
                else
                {
                    lblCountdown.Content = "0";
                    _countdownTimer.Stop();
                }
            };
        }

        public void Init(Settings settings)
        {
            _settings = settings;

            lblInitialPause.Content = _settings.InitialPause;
            lblOdorFlowDuration.Content = _settings.OdorFlowDuration;
            lblFinalPause.Content = _settings.FinalPause;

            _mfc.FreshAirSpeed = _settings.FreshAir;
            _mfc.OdorDirection = MFC.OdorFlow.None; // should I add delay here?
        }

        public void Run(int step)
        {
            _step = step;

            var timer = Utils.DispatchOnce.Do(0.1, () => 
            {
                _waitingCountdown = _settings.InitialPause > 0 ? _settings.InitialPause : _settings.OdorFlowDuration;
                _countdownTimer.Start();

                _mfc.OdorSpeed = _settings.OdorQuantities[step];
                lblOdorStatus.Content = _settings.OdorQuantities[step];

                stpInitialPause.Style = _activeIntervalStyle;

                _logger.Add(LogSource.OdProd, "trial", _settings.OdorQuantities[step].ToString());
            });

            timer.Then(_settings.InitialPause > 0 ? _settings.InitialPause : 0.1, () => StartOdorFlow());

            timer.Then(_settings.OdorFlowDuration, () => StopOdorFlow());

            timer.Then(_settings.FinalPause > 0 ? _settings.FinalPause : 0.1, () => Finilize());

            timer.Start();
        }

        // Internal

        readonly Style _inactiveIntervalStyle;
        readonly Style _activeIntervalStyle;

        Settings _settings;
        int _step = 0;

        MFC _mfc = MFC.Instance;
        Logger _logger = Logger.Instance;

        DispatcherTimer _countdownTimer = new DispatcherTimer();

        int _waitingCountdown = 0;


        private void StartOdorFlow()
        {
            _waitingCountdown = _settings.OdorFlowDuration;
            _countdownTimer.Start();

            _mfc.OdorDirection = MFC.OdorFlow.ToWaste;

            stpInitialPause.Style = _inactiveIntervalStyle;
            stpOdorFlowDuration.Style = _activeIntervalStyle;

            _logger.Add(LogSource.OdProd, "start");
        }

        private void StopOdorFlow()
        {
            if (_settings.FinalPause > 0)
            {
                _waitingCountdown = _settings.FinalPause;
                _countdownTimer.Start();
            }

            _mfc.OdorDirection = MFC.OdorFlow.None;

            stpOdorFlowDuration.Style = _inactiveIntervalStyle;
            stpFinalPause.Style = _activeIntervalStyle;

            _logger.Add(LogSource.OdProd, "stop");
        }

        private void Finilize()
        {
            stpFinalPause.Style = _inactiveIntervalStyle;
            _logger.Add(LogSource.OdProd, "finished");

            if (++_step >= _settings.OdorQuantities.Length)
            {
                Finished(this, new EventArgs());
            }
            else
            {
                Next(this, _step);
            }
        }
    }
}
