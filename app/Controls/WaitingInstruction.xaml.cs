using AutOlD2Ch.Utils;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace AutOlD2Ch.Controls;

public partial class WaitingInstruction : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    #region Text property

    [Description("Instruction text"), Category("Common Properties")]
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(WaitingInstruction),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(
            (s, e) => (s as WaitingInstruction)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(Text)))
        ))
    );

    #endregion

    public double WaitingTime
    {
        get => prbProgress.Maximum;
        set
        {
            var val = Math.Max(0, value);
            var progress = Progress;
            prbProgress.Maximum = val;
            prbProgress.Value = val * progress;
        }
    }

    public double Progress
    {
        get => prbProgress.Maximum > 0 ? prbProgress.Value / prbProgress.Maximum : 0;
        set
        {
            var val = Math.Max(0, Math.Min(value, 1));
            prbProgress.Value = WaitingTime * val;
            prbProgress.Visibility = val > 0 ? Visibility.Visible : Visibility.Hidden;
        }
    }

    public WaitingInstruction()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>
    /// Starts waiting progress
    /// </summary>
    /// <param name="duration">Waiting time. If not set, or it is not positive value, then <see cref="WaitingTime"/> value is used</param>
    public void Start(double duration = 0)
    {
        Reset();

        if (duration > 0)
        {
            WaitingTime = duration;
        }

        prbProgress.Visibility = Visibility.Visible;

        _start = Timestamp.Sec;

        _timer = DispatchOnceUI.Do(UPDATE_INTERVAL, UpdateProgress);
    }

    public void Reset()
    {
        prbProgress.Visibility = Visibility.Hidden;
        _timer?.Stop();
        _timer = null;
        Progress = 0;
    }

    // Internal

    const double UPDATE_INTERVAL = 0.1;

    double _start = 0;
    DispatchOnceUI _timer;

    private void UpdateProgress()
    {
        _timer = null;

        var duration = Timestamp.Sec - _start;
        var progress = Math.Min(1.0, duration / WaitingTime);

        Progress = progress;

        if (progress < 1.0)
        {
            _timer = DispatchOnceUI.Do(UPDATE_INTERVAL, UpdateProgress);
        }
    }
}
