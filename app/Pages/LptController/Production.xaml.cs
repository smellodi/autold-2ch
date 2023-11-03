using AutOlD2Ch.Tests.LptController;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AutOlD2Ch.Pages.LptController;

public partial class Production : Page, IPage<EventArgs>, INotifyPropertyChanged, IDisposable
{
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

    #region IsOdorFlow property

    public bool IsOdorFlow
    {
        get => _stage.HasFlag(Procedure.Stage.OdorFlow);
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

    public string Label_Channel1 => Utils.L10n.T("Channel") + " #1";
    public string Label_Channel2 => Utils.L10n.T("Channel") + " #2";

    public event EventHandler<EventArgs>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public Production()
    {
        DataContext = this;

        InitializeComponent();

        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        _procedure.Data += (s, pid) => Dispatcher.Invoke(() => lblPID.Content = pid.ToString("F2") );
        _procedure.StageChanged += (s, stage) => Dispatcher.Invoke(() => SetStage(stage));
        _procedure.Marker += (s, marker) => Dispatcher.Invoke(() => lblMarker.Content = marker);
        _procedure.Finished += (s, e) => Dispatcher.Invoke(() => FinalizeTest());
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

    public void Dispose()
    {
        _procedure.Dispose();
        GC.SuppressFinalize(this);
    }


    // Internal

    readonly Procedure _procedure = new();

    Settings? _settings;
    Procedure.Stage _stage = Procedure.Stage.None;

    private void UpdateUI()
    {
        var pulse = _procedure.CurrentPulse;

        lblOdorStatus.Content = pulse != null ? $"{pulse.Channel1?.Flow ?? 0}/{pulse.Channel2?.Flow ?? 0}" : "";

        pdsOdor1Flow.Duration = pulse?.Channel1?.GetDuration(_settings!.OdorFlowDurationMs) ?? 0;
        pdsOdor1Flow.Delay = pulse?.Channel1?.Delay ?? 0;
        pdsOdor2Flow.Duration = pulse?.Channel2?.GetDuration(_settings!.OdorFlowDurationMs) ?? 0;
        pdsOdor2Flow.Delay = pulse?.Channel2?.Delay ?? 0;
    }

    private void SetStage(Procedure.Stage stage)
    {
        bool isOdorFlowContinuing =
            (_stage.HasFlag(Procedure.Stage.Odor1Flow) || _stage.HasFlag(Procedure.Stage.Odor2Flow)) &&
            (stage.HasFlag(Procedure.Stage.Odor1Flow) || stage.HasFlag(Procedure.Stage.Odor2Flow));

        _stage = stage;

        var pause = _stage switch
        {
            (Procedure.Stage.Odor1Flow | Procedure.Stage.OdorFlow) or
            (Procedure.Stage.Odor2Flow | Procedure.Stage.OdorFlow) or
            (Procedure.Stage.Odor1Flow | Procedure.Stage.Odor2Flow | Procedure.Stage.OdorFlow) => 
                _settings!.OdorFlowDuration,
            Procedure.Stage.None => 0,
            Procedure.Stage.OdorFlow => 0,
            _ => throw new NotImplementedException($"Stage '{_stage}' of LTP Controller does not exist")
        };

        IsOdor1Flow = _stage.HasFlag(Procedure.Stage.Odor1Flow);
        IsOdor2Flow = _stage.HasFlag(Procedure.Stage.Odor2Flow);
        IsOdorFlow = _stage.HasFlag(Procedure.Stage.OdorFlow);

        if (pause > 1 && !isOdorFlowContinuing)
        {
            wtiWaiting.Start(pause);
        }
        else if (!IsOdorFlow)
        {
            wtiWaiting.Reset();
            lblMarker.Content = "-";
        }

        UpdateUI();
    }

    private void FinalizeTest()
    {
        SetStage(Procedure.Stage.None);

        Next?.Invoke(this, new EventArgs());
    }


    // UI

    private void Interrupt_Click(object sender, RoutedEventArgs e)
    {
        // Do I need to show a confirmation dialog here?
        _procedure.Stop();
        Next?.Invoke(this, new EventArgs());
    }
}
