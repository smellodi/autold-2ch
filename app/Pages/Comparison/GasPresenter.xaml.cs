using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AutOlD2Ch.Tests.Comparison;

namespace AutOlD2Ch.Pages.Comparison
{
    public partial class GasPresenter : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next;

        public GasPresenter()
        {
            DataContext = this;

            InitializeComponent();

            Storage.Instance
                .BindScaleToZoomLevel(sctScale)
                .BindContentToZoomLevel(lblZoom)
                .BindVisibilityToDebug(lblDebug);

            _procedure.Data += (s, pid) => Dispatcher.Invoke(() => lblPID.Content = pid.ToString("F2") );
            _procedure.StageChanged += (s, stage) => Dispatcher.Invoke(() => SetStage(stage));
            _procedure.RequestAnswer += (s, _) => Dispatcher.Invoke(() => SaveScanResults());
            _procedure.Finished += (s, noMoreTrials) => Dispatcher.Invoke(() => FinilizeTrial(noMoreTrials));
            _procedure.DNSError += (s, description) => Dispatcher.Invoke(() => DisplayDNSError(description));
        }

        public void Init(Settings settings)
        {
            _settings = settings;

            _procedure.Start(settings, Stage.Test);
        }

        public void Interrupt()
        {
            _procedure.Stop();
        }


        // Internal

        readonly Procedure _procedure = new();

        Settings _settings;

        private void SetStage(Procedure.Stage stage)
        {
            var isOdorFlowing = stage.OutputValveStage == Procedure.OutputValveStage.Opened;
            double pause = isOdorFlowing ? _settings.OdorFlowDuration : _settings.InitialPause;

            wtiWaiting.Visibility = isOdorFlowing || stage.MixtureID == Procedure.MixtureID.None ? Visibility.Hidden : Visibility.Visible;

            if (!isOdorFlowing)
            {
                lblGas1.IsEnabled = false;
                lblGas2.IsEnabled = false;

                if (stage.MixtureID != Procedure.MixtureID.None)
                {
                    wtiWaiting.Start(pause);
                }
            }
            else if (stage.MixtureID == Procedure.MixtureID.First)
            {
                lblGas1.IsEnabled = true;
                wtiOdor1.Start(pause);
            }
            else if (stage.MixtureID == Procedure.MixtureID.Second)
            {
                lblGas2.IsEnabled = true;
                wtiOdor2.Start(pause);
            }
        }

        private void SaveScanResults()
        {
            wtiOdor1.Progress = 0;
            wtiOdor2.Progress = 0;

            _procedure.SetResult(Procedure.Answer.None);
        }

        private void FinilizeTrial(bool noMoreTrials)
        {
            if (noMoreTrials)
            {
                Next?.Invoke(this, new EventArgs());
            }
            else
            {
                Utils.DispatchOnceUI.Do(0.1, () => _procedure.Next());
            }
        }

        private void DisplayDNSError(string error)
        {
            var margin = new Thickness(0, 2, 0, 2);
            var padding = new Thickness(32, 6, 32, 6);
            var colorError = new SolidColorBrush(Color.FromRgb(0xff, 0xc8, 0xc0));

            var lblMsg = new Label()
            {
                Content = error,
                Padding = padding,
                Margin = margin,
                //HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = colorError
            };

            stpDMSErrors.Children.Add(lblMsg);
            scvDMSErrors.ScrollToEnd();
        }


        // UI

        private void Interrupt_Click(object sender, RoutedEventArgs e)
        {
            // Do I need to show a confirmation dialog here?
            _procedure.Stop();
            Next?.Invoke(this, new EventArgs());
        }
    }
}
