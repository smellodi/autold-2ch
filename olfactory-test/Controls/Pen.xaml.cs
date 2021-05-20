using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Olfactory.Controls
{
    public partial class Pen : UserControl
    {
        #region ID property

        [Description("Pen ID"), Category("Common Properties")]
        public int ID
        {
            get { return (int)GetValue(IDProperty); }
            set
            {
                lblPen.Content = $"Pen #{value}";
                SetValue(IDProperty, value);
            }
        }

        public static readonly DependencyProperty IDProperty = DependencyProperty.Register(
            "ID",
            typeof(int),
            typeof(Pen),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnIDPropertyChanged)));

        private static void OnIDPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Pen instance)
            {
                instance.lblPen.Content = $"Pen #{e.NewValue}";
            }
        }

        #endregion 

        #region IsActive property

        [Description("Is the pen active"), Category("Common Properties")]
        public bool IsActive
        {
            get { return (bool)GetValue(IsActiveProperty); }
            set
            {
                lblPen.Style = value ? _activePenStyle : _inactivePenStyle;
                SetValue(IsActiveProperty, value);
            }
        }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
            "IsActive",
            typeof(bool),
            typeof(Pen),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnIsActivePropertyChanged)));

        private static void OnIsActivePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Pen instance)
            {
                instance.lblPen.Style = instance.IsActive ? instance._activePenStyle : instance._inactivePenStyle;
            }
        }

        #endregion

        #region IsColorVisible property

        [Description("Is pen color visible"), Category("Common Properties")]
        public bool IsColorVisible
        {
            get { return (bool)GetValue(IsColorVisibleProperty); }
            set
            {
                rctPenColor.Visibility = value ? Visibility.Visible : Visibility.Hidden;
                SetValue(IsColorVisibleProperty, value);
            }
        }

        public static readonly DependencyProperty IsColorVisibleProperty = DependencyProperty.Register(
            "IsColorVisible",
            typeof(bool),
            typeof(Pen),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnIsColorVisiblePropertyChanged)));

        private static void OnIsColorVisiblePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Pen instance)
            {
                instance.rctPenColor.Visibility = instance.IsColorVisible ? Visibility.Visible : Visibility.Hidden;
            }
        }

        #endregion 

        #region IsSelectable property

        [Description("Is pen can be selected"), Category("Common Properties")]
        public bool IsSelectable
        {
            get { return (bool)GetValue(IsSelectableProperty); }
            set
            {
                btnChoice.Visibility = value ? Visibility.Visible : Visibility.Hidden;
                SetValue(IsSelectableProperty, value);
            }
        }

        public static readonly DependencyProperty IsSelectableProperty = DependencyProperty.Register(
            "IsSelectable",
            typeof(bool),
            typeof(Pen),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnIsSelectablePropertyChanged)));

        private static void OnIsSelectablePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Pen instance)
            {
                instance.btnChoice.Visibility = instance.IsSelectable ? Visibility.Visible : Visibility.Hidden;
            }
        }

        #endregion 
        
        public Tests.ThresholdTest.Pen PenInstance
        {
            get => _pen;
            set
            {
                _pen = value;
                rctPenColor.Fill = new SolidColorBrush(GetPenColor(value.Color));
            }
        }

        public event EventHandler Selected = delegate { };
        
        public Pen()
        {
            InitializeComponent();

            var penStyle = FindResource("Pen") as Style;

            _inactivePenStyle = new Style(typeof(Label), penStyle);
            _inactivePenStyle.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 255, 255))));

            _activePenStyle = new Style(typeof(Label), penStyle);
            _activePenStyle.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(102, 205, 255))));

            lblPen.Style = IsActive ? _activePenStyle : _inactivePenStyle;
            rctPenColor.Visibility = IsColorVisible ? Visibility.Visible : Visibility.Hidden;
            btnChoice.Visibility = IsSelectable ? Visibility.Visible : Visibility.Hidden;
        }


        // Internal

        Tests.ThresholdTest.Pen _pen;

        readonly Style _inactivePenStyle;
        readonly Style _activePenStyle;

        private void OnChoice_Click(object sender, RoutedEventArgs e)
        {
            Selected(this, new EventArgs());
        }

        private Color GetPenColor(Tests.ThresholdTest.PenColor color) => color switch
        {
            Tests.ThresholdTest.PenColor.Red => Colors.Red,
            Tests.ThresholdTest.PenColor.Green => Colors.Green,
            Tests.ThresholdTest.PenColor.Blue => Colors.Blue,
            _ => throw new NotImplementedException("Unrecognized pen color"),
        };
    }
}