using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Olfactory.Controls
{
    public partial class ChannelIndicator : UserControl, INotifyPropertyChanged
    {
        public enum DataSource { CleanAir, ScentedAir1, ScentedAir2, PID, Loop }

        #region Title property

        [Description("Title"), Category("Common Properties")]
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(ChannelIndicator),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnTitlePropertyChanged)));

        private static void OnTitlePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ChannelIndicator instance)
            {
                instance.PropertyChanged(instance, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        #endregion 

        #region Value property

        [Description("Value"), Category("Common Properties")]
        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(ChannelIndicator),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnValuePropertyChanged)));

        private static void OnValuePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ChannelIndicator instance)
            {
                instance.PropertyChanged(instance, new PropertyChangedEventArgs(nameof(Value)));
                instance.PropertyChanged(instance, new PropertyChangedEventArgs(nameof(ValueStr)));
            }
        }

        #endregion 

        #region Units property

        [Description("Units"), Category("Common Properties")]
        public string Units
        {
            get => (string)GetValue(UnitsProperty);
            set => SetValue(UnitsProperty, value);
        }

        public static readonly DependencyProperty UnitsProperty = DependencyProperty.Register(
            nameof(Units),
            typeof(string),
            typeof(ChannelIndicator),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnUnitsPropertyChanged)));

        private static void OnUnitsPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ChannelIndicator instance)
            {
                instance.PropertyChanged(instance, new PropertyChangedEventArgs(nameof(Units)));
            }
        }

        #endregion 

        #region IsActive property

        [Description("Is the indicator active"), Category("Common Properties")]
        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(ChannelIndicator),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnIsActivePropertyChanged)));

        private static void OnIsActivePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ChannelIndicator instance)
            {
                instance.PropertyChanged(instance, new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }

        #endregion

        #region Precision property

        [Description("Precision"), Category("Common Properties")]
        public int Precision
        {
            get => (int)GetValue(PrecisionProperty);
            set => SetValue(PrecisionProperty, Math.Max(0, value));
        }

        public static readonly DependencyProperty PrecisionProperty = DependencyProperty.Register(
            nameof(Precision),
            typeof(int),
            typeof(ChannelIndicator),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnPrecisionPropertyChanged)));

        private static void OnPrecisionPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ChannelIndicator instance)
            {
                instance.PropertyChanged(instance, new PropertyChangedEventArgs(nameof(Precision)));
                instance.PropertyChanged(instance, new PropertyChangedEventArgs(nameof(ValueStr)));
            }
        }

        #endregion

        #region Source property

        [Description("Data source"), Category("Common Properties")]
        public DataSource Source
        {
            get => (DataSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(DataSource),
            typeof(ChannelIndicator));

        #endregion

        public string ValueStr => Value.ToString($"F{Precision}");

        public event PropertyChangedEventHandler PropertyChanged;

        public ChannelIndicator()
        {
            InitializeComponent();
        }
    }
}
