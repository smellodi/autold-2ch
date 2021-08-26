using Olfactory.Tests.OdorProduction;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Olfactory.Pages.OdorProduction
{
    public partial class Production : Page, IPage<EventArgs>, INotifyPropertyChanged
    {
        #region Stage property

        public Procedure.Stage Stage
        {
            get => _stage;
            set
            {
                _stage = value;
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(Stage)));

                var pause = value switch
                {
                    Procedure.Stage.InitWait => _settings.InitialPause,
                    Procedure.Stage.OdorFlow => _settings.OdorFlowDuration,
                    Procedure.Stage.FinalWait => _settings.FinalPause,
                    Procedure.Stage.None => 0,
                    _ => throw new NotImplementedException($"Stage '{_stage}' of Odour Pulses does not exist")
                };

                if (pause > 1)
                {
                    wtiWaiting.Start(pause);
                }
            }
        }

        #endregion 

        public event EventHandler<EventArgs> Next = delegate { };
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        public Tests.ITestEmulator Emulator => _procedure;

        public Production()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            DataContext = this;

            _procedure.Data += (s, pid) => Dispatcher.Invoke(() => lblPID.Content = pid.ToString("F2") );
            _procedure.StageChanged += (s, stage) => Dispatcher.Invoke(() => Stage = stage);
            _procedure.Finished += (s, noMoreTrials) => Dispatcher.Invoke(() => FinilizeTrial(noMoreTrials));
        }

        public void Init(Settings settings)
        {
            _settings = settings;

            lblOdorStatus.Content = _settings.OdorQuantities[0];

            lblInitialPause.Content = $"{_settings.InitialPause} sec";
            lblOdorFlowDuration.Content = $"{_settings.OdorFlowDuration} sec";
            lblFinalPause.Content = $"{_settings.FinalPause} sec";

            _procedure.Start(settings);
        }

        public void Interrupt()
        {
            _procedure.Interrupt();
        }


        // Internal

        Settings _settings;
        Procedure _procedure = new Procedure();
        Procedure.Stage _stage = Procedure.Stage.None;

        private void FinilizeTrial(bool noMoreTrials)
        {
            Stage = Procedure.Stage.None;

            if (noMoreTrials)
            {
                Next(this, new EventArgs());
            }
            else
            {
                lblOdorStatus.Content = _settings.OdorQuantities[_procedure.Step];
                Utils.DispatchOnceUI.Do(0.1, () => _procedure.Next());
            }
        }


        // UI

        private void btnInterrupt_Click(object sender, RoutedEventArgs e)
        {
            // Do I need to show a confirmation dialog here?
            _procedure.Interrupt();
            Next(this, new EventArgs());
        }
    }
}
