using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AutOlD2Ch.Controls;

public partial class Scale9 : UserControl, INotifyPropertyChanged
{
    #region Title property

    [Description("Title"), Category("Common Properties")]
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set
        {
            txbTitle.Text = value;
            SetValue(TitleProperty, value);
        }
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        "Title",
        typeof(string),
        typeof(Scale9),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(TitleProperty_Changed)));

    private static void TitleProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is Scale9 instance)
        {
            instance.txbTitle.Text = (string)e.NewValue;
        }
    }

    #endregion

    #region Description property

    [Description("Description"), Category("Common Properties")]
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set
        {
            txbDescription.Text = value;
            SetValue(DescriptionProperty, value);
        }
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        "Description",
        typeof(string),
        typeof(Scale9),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(DescriptionProperty_Changed)));

    private static void DescriptionProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is Scale9 instance)
        {
            instance.txbDescription.Text = (string)e.NewValue;
        }
    }

    #endregion

    #region LeftValue property

    [Description("Left, or minimum value"), Category("Common Properties")]
    public string LeftValue
    {
        get => (string)GetValue(LeftValueProperty);
        set
        {
            lblLeftValue.Text = value;
            SetValue(LeftValueProperty, value);
        }
    }

    public static readonly DependencyProperty LeftValueProperty = DependencyProperty.Register(
        "LeftValue",
        typeof(string),
        typeof(Scale9),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(LeftValueProperty_Changed)));

    private static void LeftValueProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is Scale9 instance)
        {
            instance.lblLeftValue.Text = (string)e.NewValue;
        }
    }

    #endregion

    #region RightValue property

    [Description("Right, or maximum value"), Category("Common Properties")]
    public string RightValue
    {
        get => (string)GetValue(RightValueProperty);
        set
        {
            lblRightValue.Text = value;
            SetValue(RightValueProperty, value);
        }
    }

    public static readonly DependencyProperty RightValueProperty = DependencyProperty.Register(
        "RightValue",
        typeof(string),
        typeof(Scale9),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(RightValueProperty_Changed)));

    private static void RightValueProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is Scale9 instance)
        {
            instance.lblRightValue.Text = (string)e.NewValue;
        }
    }

    #endregion

    #region IsValueBarVisible property

    [Description("Is value bar visible?"), Category("Common Properties")]
    public bool IsValueBarVisible
    {
        get => (bool)GetValue(IsValueBarVisibleProperty);
        set => SetValue(IsValueBarVisibleProperty, value);
    }

    public static readonly DependencyProperty IsValueBarVisibleProperty = DependencyProperty.Register(
        "IsValueBarVisible",
        typeof(bool),
        typeof(Scale9),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(IsValueBarVisibleProperty_Changed)));

    private static void IsValueBarVisibleProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is Scale9 instance)
        {
            instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(IsValueBarVisible)));
            System.Diagnostics.Debug.WriteLine(e.NewValue);
        }
    }

    #endregion

    public event EventHandler<RoutedEventArgs> ValueChanged = delegate { };
    public event PropertyChangedEventHandler PropertyChanged;

    public int? Value
    {
        get
        {
            RadioButton rdb = grdScale.Children.OfType<RadioButton>().FirstOrDefault(item => item.IsChecked ?? false);
            var value = rdb?.DataContext as string;
            return value == null ? null : int.Parse(value);
        }
        set
        {
            var rdbs = grdScale.Children.OfType<RadioButton>();
            if (0 <= value && value < rdbs.Count())
            {
                rdbs.ElementAt((int)value).IsChecked = true;
            }
        }
    }

    public Scale9()
    {
        InitializeComponent();

        string group = _rnd.Next().ToString();

        if (!string.IsNullOrEmpty(Name))
        {
            group = Name;
        }
        else if (!string.IsNullOrEmpty((string)DataContext))
        {
            group = (string)DataContext;
        }

        foreach (object ctrl in grdScale.Children)
        {
            if (ctrl is RadioButton)
            {
                (ctrl as RadioButton).GroupName = group;
            }
        }
    }

    // Internal

    static readonly Random _rnd = new((int)DateTime.Now.Ticks);

    private void OnStateChanged(object sender, RoutedEventArgs e)
    {
        ValueChanged(this, new RoutedEventArgs(e.RoutedEvent));
    }
}
