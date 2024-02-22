using AutOlD2Ch.Tests.OdorProduction;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AutOlD2Ch.Pages.OdorProduction;

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

    #region IsOdor1Flow property

    public bool IsOdor1Flow
    {
        get => _stage.HasFlag(Procedure.Stage.Odor1Flow);
        set => SetValue(IsOdor1FlowProperty, value);
    }

    public static readonly DependencyProperty IsOdor1FlowProperty = DependencyProperty.Register(
        nameof(IsOdor1Flow),
        typeof(bool),
        typeof(Production),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(
            (s, e) => (s as Production)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsOdor1Flow)))
        ))
    );

    #endregion

    #region IsOdor2Flow property

    public bool IsOdor2Flow
    {
        get => _stage.HasFlag(Procedure.Stage.Odor2Flow);
        set => SetValue(IsOdor2FlowProperty, value);
    }

    public static readonly DependencyProperty IsOdor2FlowProperty = DependencyProperty.Register(
        nameof(IsOdor2Flow),
        typeof(bool),
        typeof(Production),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(
            (s, e) => (s as Production)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsOdor2Flow)))
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

    public bool IsOdorFlowPhase
    {
        get => _isOdorFlowPhase;
        set
        {
            _isOdorFlowPhase = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOdorFlowPhase)));
        }
    }

    public string Label_Channel1 => Utils.L10n.T("Channel") + " #1";
    public string Label_Channel2 => Utils.L10n.T("Channel") + " #2";

    public event EventHandler<EventArgs>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public Tests.ITestEmulator Emulator => _procedure;

    public Production()
    {
        DataContext = this;

        InitializeComponent();

        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        _procedure.Data += (s, pid) => Dispatcher.Invoke(() => lblPID.Content = pid.ToString("F2"));
        _procedure.StageChanged += (s, stage) => Dispatcher.Invoke(() => SetStage(stage));
        _procedure.Finished += (s, noMoreTrials) => Dispatcher.Invoke(() => FinalizeTrial(noMoreTrials));
    }

    public void Init(Settings settings)
    {
        _settings = settings;

        pdsInitialPause.Duration = _settings.InitialPause * 1000;
        pdsFinalPause.Duration = _settings.FinalPause * 1000;

        _procedure.Start(settings);

        UpdateUI();
    }

    public void Interrupt()
    {
        _procedure.Stop();
    }


    // Internal

    readonly Procedure _procedure = new();

    Settings? _settings;
    Procedure.Stage _stage = Procedure.Stage.None;

    bool _isOdorFlowPhase = false;

    private void UpdateUI()
    {
        if (_settings == null)
            return;

        var pulse = _settings.Pulses[_procedure.Step];
        lblOdorStatus.Content = $"{pulse.Channel1?.Flow ?? 0}/{pulse.Channel2?.Flow ?? 0}";

        pdsOdor1Flow.Duration = pulse.Channel1?.GetDuration(_settings.OdorFlowDurationMs) ?? 0;
        pdsOdor1Flow.Delay = pulse.Channel1?.Delay ?? 0;
        pdsOdor2Flow.Duration = pulse.Channel2?.GetDuration(_settings.OdorFlowDurationMs) ?? 0;
        pdsOdor2Flow.Delay = pulse.Channel2?.Delay ?? 0;
    }

    private void SetStage(Procedure.Stage stage)
    {
        if (_settings == null)
            return;

        bool isOdorFlowContinuing =
            (_stage.HasFlag(Procedure.Stage.Odor1Flow) || _stage.HasFlag(Procedure.Stage.Odor2Flow)) &&
            (stage.HasFlag(Procedure.Stage.Odor1Flow) || stage.HasFlag(Procedure.Stage.Odor2Flow));

        _stage = stage;

        var pause = _stage switch
        {
            Procedure.Stage.InitWait => _settings.InitialPause,
            (Procedure.Stage.Odor1Flow | Procedure.Stage.OdorFlow) or
            (Procedure.Stage.Odor2Flow | Procedure.Stage.OdorFlow) or
            (Procedure.Stage.Odor1Flow | Procedure.Stage.Odor2Flow | Procedure.Stage.OdorFlow) =>
                _settings.OdorFlowDuration,
            Procedure.Stage.OdorFlow => 0,
            Procedure.Stage.FinalWait => _settings.FinalPause,
            Procedure.Stage.None => 0,
            _ => throw new NotImplementedException($"Stage '{_stage}' of Odour Pulses does not exist")
        };

        IsInitialPause = _stage == Procedure.Stage.InitWait;
        IsOdor1Flow = _stage.HasFlag(Procedure.Stage.Odor1Flow);
        IsOdor2Flow = _stage.HasFlag(Procedure.Stage.Odor2Flow);
        IsFinalPause = _stage == Procedure.Stage.FinalWait;

        IsOdorFlowPhase = _stage.HasFlag(Procedure.Stage.OdorFlow);

        if (pause > 1 && !isOdorFlowContinuing)
        {
            wtiWaiting.Start(pause);
        }
    }

    private void FinalizeTrial(bool noMoreTrials)
    {
        SetStage(Procedure.Stage.None);

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
}
