using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Olfactory.Tests.OdorProduction;

namespace Olfactory.Pages.OdorProduction
{
    public partial class Production : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next = delegate { };

        public Tests.ITestEmulator Emulator => _procedure;

        public Production()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            _inactiveIntervalStyle = FindResource("Interval") as Style;

            _activeIntervalStyle = new Style(typeof(StackPanel), _inactiveIntervalStyle);
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

            _procedure.Data += (s, pid) =>
            {
                lblPID.Content = pid.ToString("F2");
            };
            _procedure.StageChanged += (s, stage) =>
            {
                switch (stage)
                {
                    case Procedure.Stage.InitWait:
                        stpInitialPause.Style = _activeIntervalStyle;
                        InitiateCountdownTimer(_settings.InitialPause);
                        break;
                    case Procedure.Stage.OdorFlow:
                        stpInitialPause.Style = _inactiveIntervalStyle;
                        stpOdorFlowDuration.Style = _activeIntervalStyle;
                        InitiateCountdownTimer(_settings.OdorFlowDuration);
                        break;
                    case Procedure.Stage.FinalWait:
                        stpOdorFlowDuration.Style = _inactiveIntervalStyle;
                        stpFinalPause.Style = _activeIntervalStyle;
                        InitiateCountdownTimer(_settings.FinalPause);
                        break;
                    default:
                        throw new NotImplementedException($"Trial stage '{stage}' of Odor Production does not exist");
                }
            };
            _procedure.Finished += (s, noMoreTrials) =>
            {
                stpFinalPause.Style = _inactiveIntervalStyle;

                if (noMoreTrials)
                {
                    Next(this, new EventArgs());
                }
                else
                {
                    lblOdorStatus.Content = _settings.OdorQuantities[_procedure.Step];
                    Utils.DispatchOnce.Do(0.1, () => _procedure.Next());
                }
            };
        }

        public void Init(Settings settings)
        {
            _settings = settings;

            lblOdorStatus.Content = _settings.OdorQuantities[0];
            lblInitialPause.Content = _settings.InitialPause;
            lblOdorFlowDuration.Content = _settings.OdorFlowDuration;
            lblFinalPause.Content = _settings.FinalPause;

            _procedure.Start(settings);
        }


        // Internal

        readonly Style _inactiveIntervalStyle;
        readonly Style _activeIntervalStyle;

        Settings _settings;
        Procedure _procedure = new Procedure();

        DispatcherTimer _countdownTimer = new DispatcherTimer();

        int _waitingCountdown = 0;


        private void InitiateCountdownTimer(int value)
        {
            _countdownTimer.Stop();
            _waitingCountdown = value;
            lblCountdown.Content = _waitingCountdown;

            if (value > 0)
            {
                _countdownTimer.Start();
            }
        }
    }
}
