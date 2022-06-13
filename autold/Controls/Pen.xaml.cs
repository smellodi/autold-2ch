using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Olfactory.Controls
{
    public partial class Pen : UserControl, INotifyPropertyChanged
    {
        #region ID property

        [Description("Pen ID"), Category("Common Properties")]
        public string ID
        {
            get => (string)GetValue(IDProperty);
            set => SetValue(IDProperty, value);
        }

        public static readonly DependencyProperty IDProperty = DependencyProperty.Register(
            nameof(ID),
            typeof(string),
            typeof(Pen),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as Pen)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(ID)))
            ))
        );

        #endregion 

        #region IsActive property

        [Description("Is the pen active"), Category("Common Properties")]
        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(Pen),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as Pen)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsActive)))
            ))
        );

        #endregion

        #region IsColorVisible property

        [Description("Is pen color visible"), Category("Common Properties")]
        public bool IsColorVisible
        {
            get => (bool)GetValue(IsColorVisibleProperty);
            set => SetValue(IsColorVisibleProperty, value);
        }

        public static readonly DependencyProperty IsColorVisibleProperty = DependencyProperty.Register(
            nameof(IsColorVisible),
            typeof(bool),
            typeof(Pen),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as Pen)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsColorVisible)))
            ))
        );

        #endregion 

        #region IsSelectable property

        [Description("Can the pen be selected"), Category("Common Properties")]
        public bool IsSelectable
        {
            get => (bool)GetValue(IsSelectableProperty);
            set => SetValue(IsSelectableProperty, value);
        }

        public static readonly DependencyProperty IsSelectableProperty = DependencyProperty.Register(
            nameof(IsSelectable),
            typeof(bool),
            typeof(Pen),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as Pen)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsSelectable)))
            ))
        );

        #endregion 

        #region CanChoose property

        [Description("Has the pen one or two buttons"), Category("Common Properties")]
        public bool CanChoose
        {
            get => (bool)GetValue(CanChooseProperty);
            set => SetValue(CanChooseProperty, value);
        }

        public static readonly DependencyProperty CanChooseProperty = DependencyProperty.Register(
            nameof(CanChoose),
            typeof(bool),
            typeof(Pen),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as Pen)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(CanChoose)))
            ))
        );

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

        public event EventHandler<bool> Selected;
        public event PropertyChangedEventHandler PropertyChanged;

        public Pen()
        {
            InitializeComponent();

            DataContext = this;
        }


        // Internal

        Tests.ThresholdTest.Pen _pen;

        private Color GetPenColor(Tests.ThresholdTest.PenColor color) => color switch
        {
            Tests.ThresholdTest.PenColor.Odor => Colors.Red,
            Tests.ThresholdTest.PenColor.NonOdor => Colors.Blue,
            _ => throw new NotImplementedException("Unrecognized pen color"),
        };

        // UI

        private void OnChoice_Click(object sender, RoutedEventArgs e)
        {
            bool answer = true;
            if (CanChoose)
            {
                answer = (sender as Button).Tag.Equals("Yes");
            }
            Selected?.Invoke(this, answer);
        }
    }
}