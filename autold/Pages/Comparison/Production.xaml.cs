using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Olfactory.Tests.Comparison;

namespace Olfactory.Pages.Comparison
{
    public partial class Production : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next;

        public Tests.ITestEmulator Emulator => _procedure;

        public Production()
        {
            DataContext = this;

            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            _procedure.Data += (s, pid) => Dispatcher.Invoke(() => lblPID.Content = pid.ToString("F2") );
            _procedure.StageChanged += (s, stage) => Dispatcher.Invoke(() => SetStage(stage));
            _procedure.RequestAnswer += (s, _) => Dispatcher.Invoke(() => RequestAnswer());
            _procedure.Finished += (s, noMoreTrials) => Dispatcher.Invoke(() => FinilizeTrial(noMoreTrials));
        }

        public void Init(Settings settings)
        {
            _settings = settings;

            _procedure.Start(settings);

            UpdateUI();
        }

        public void Interrupt()
        {
            _procedure.Stop();
        }


        // Internal

        readonly Procedure _procedure = new();

        Settings _settings;

        private void UpdateUI()
        {
        }

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

        private void RequestAnswer()
        {
            wtiOdor1.Progress = 0;
            wtiOdor2.Progress = 0;
            stpAnswer.Visibility = Visibility.Visible;
        }

        private void FinilizeTrial(bool noMoreTrials)
        {
            stpAnswer.Visibility = Visibility.Hidden;

            if (noMoreTrials)
            {
                Next?.Invoke(this, new EventArgs());
            }
            else
            {
                UpdateUI();
                Utils.DispatchOnceUI.Do(0.1, () => _procedure.Next());
            }
        }


        // UI

        private void Interrupt_Click(object sender, RoutedEventArgs e)
        {
            // Do I need to show a confirmation dialog here?
            _procedure.Stop();
            Next?.Invoke(this, new EventArgs());
        }

        private void Same_Click(object sender, RoutedEventArgs e)
        {
            _procedure.SetResult(Procedure.Answer.Same);
        }

        private void Different_Click(object sender, RoutedEventArgs e)
        {
            _procedure.SetResult(Procedure.Answer.Different);
        }
    }
}
