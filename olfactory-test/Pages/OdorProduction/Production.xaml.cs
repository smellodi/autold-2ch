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
        #region IsInitialPause property

        public bool IsInitialPause
        {
            get => _stage == Procedure.Stage.InitWait;
            set => SetValue(IsInitialPauseProperty, value);
        }

        public static readonly DependencyProperty IsInitialPauseProperty = DependencyProperty.Register(
            nameof(IsInitialPause),
            typeof(bool),
            typeof(Production),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as Production)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsInitialPause)))
            ))
        );

        #endregion 

        #region IsOdorFlow property

        public bool IsOdorFlow
        {
            get => _stage == Procedure.Stage.OdorFlow;
            set => SetValue(IsOdorFlowProperty, value);
        }

        public static readonly DependencyProperty IsOdorFlowProperty = DependencyProperty.Register(
            nameof(IsOdorFlow),
            typeof(bool),
            typeof(Production),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as Production)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsOdorFlow)))
            ))
        );

        #endregion 

        #region IsFinalPause property

        public bool IsFinalPause
        {
            get => _stage == Procedure.Stage.FinalWait;
            set => SetValue(IsFinalPauseProperty, value);
        }

        public static readonly DependencyProperty IsFinalPauseProperty = DependencyProperty.Register(
            nameof(IsFinalPause),
            typeof(bool),
            typeof(Production),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as Production)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsFinalPause)))
            ))
        );

        #endregion 

        public event EventHandler<EventArgs> Next;
        public event PropertyChangedEventHandler PropertyChanged;

        public Tests.ITestEmulator Emulator => _procedure;

        public Production()
        {
            DataContext = this;

            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            _procedure.Data += (s, pid) => Dispatcher.Invoke(() => lblPID.Content = pid.ToString("F2") );
            _procedure.StageChanged += (s, stage) => Dispatcher.Invoke(() => SetStage(stage));
            _procedure.Finished += (s, noMoreTrials) => Dispatcher.Invoke(() => FinilizeTrial(noMoreTrials));
        }

        public void Init(Settings settings)
        {
            _settings = settings;

            lblOdorStatus.Content = _settings.OdorQuantities[0];

            pdsInitialPause.Value = _settings.InitialPause;
            pdsOdorFlow.Value = _settings.OdorFlowDuration;
            pdsFinalPause.Value = _settings.FinalPause;

            _procedure.Start(settings);
        }

        public void Interrupt()
        {
            _procedure.Interrupt();
        }


        // Internal

        Settings _settings;
        readonly Procedure _procedure = new();
        Procedure.Stage _stage = Procedure.Stage.None;

        private void SetStage(Procedure.Stage stage)
        {
            _stage = stage;

            var pause = _stage switch
            {
                Procedure.Stage.InitWait => _settings.InitialPause,
                Procedure.Stage.OdorFlow => _settings.OdorFlowDuration,
                Procedure.Stage.FinalWait => _settings.FinalPause,
                Procedure.Stage.None => 0,
                _ => throw new NotImplementedException($"Stage '{_stage}' of Odour Pulses does not exist")
            };

            IsInitialPause = _stage == Procedure.Stage.InitWait;
            IsOdorFlow = _stage == Procedure.Stage.OdorFlow;
            IsFinalPause = _stage == Procedure.Stage.FinalWait;

            if (pause > 1)
            {
                wtiWaiting.Start(pause);
            }
        }

        private void FinilizeTrial(bool noMoreTrials)
        {
            SetStage(Procedure.Stage.None);

            if (noMoreTrials)
            {
                Next?.Invoke(this, new EventArgs());
            }
            else
            {
                lblOdorStatus.Content = _settings.OdorQuantities[_procedure.Step];
                Utils.DispatchOnceUI.Do(0.1, () => _procedure.Next());
            }
        }


        // UI

        private void Interrupt_Click(object sender, RoutedEventArgs e)
        {
            // Do I need to show a confirmation dialog here?
            _procedure.Interrupt();
            Next?.Invoke(this, new EventArgs());
        }
    }
}
