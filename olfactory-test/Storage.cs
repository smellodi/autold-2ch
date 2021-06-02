using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Olfactory
{
    /// <summary>
    /// Cross-app storage, mainly used to share the app state
    /// </summary>
    public class Storage : INotifyPropertyChanged, IDisposable
    {
        // Instance
        public static Storage Instance => _instance = _instance ?? new();

        // Event

        public enum Data
        {
            IsDebugging,
            ZoomLevel,
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        // Variables

        public bool IsDebugging
        {
            get => _isDebugging;
            set
            {
                if (_isDebugging != value)
                {
                    _isDebugging = value;
                    PropertyChanged(this, new PropertyChangedEventArgs(nameof(IsDebugging)));
                }
            }
        }

        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (_zoomLevel != value)
                {
                    _zoomLevel = value;
                    PropertyChanged(this, new PropertyChangedEventArgs(nameof(ZoomLevel)));
                }
            }
        }

        // Actions

        public void ZoomIn()
        {
            ZoomLevel = Utils.MathExt.Limit(_zoomLevel + ZOOM_STEP, ZOOM_MIN, ZOOM_MAX);
        }

        public void ZoomOut()
        {
            ZoomLevel = Utils.MathExt.Limit(_zoomLevel - ZOOM_STEP, ZOOM_MIN, ZOOM_MAX);
        }

        // Helpers

        public void BindVisibilityToDebug(DependencyObject obj)
        {
            var isDebuggingBinding = new Binding("IsDebugging");
            isDebuggingBinding.Source = this;
            isDebuggingBinding.Converter = new BooleanToVisibilityConverter();
            BindingOperations.SetBinding(obj, UIElement.VisibilityProperty, isDebuggingBinding);
        }

        public void BindScaleToZoomLevel(DependencyObject obj)
        {
            var zoomLevelBinding = new Binding("ZoomLevel");
            zoomLevelBinding.Source = this;
            BindingOperations.SetBinding(obj, ScaleTransform.ScaleXProperty, zoomLevelBinding);
            BindingOperations.SetBinding(obj, ScaleTransform.ScaleYProperty, zoomLevelBinding);
        }

        // Other

        public void Dispose()
        {
            var settings = Properties.Settings.Default;

            settings.App_ZoomLevel = _zoomLevel;

            settings.Save();
        }

        // Internal data

        static Storage _instance;

        const double ZOOM_MIN = 0.8;
        const double ZOOM_MAX = 2.0;
        const double ZOOM_STEP = 0.1;

        bool _isDebugging = false;
        double _zoomLevel = 1;

        private Storage() 
        {
            var settings = Properties.Settings.Default;

            _zoomLevel = settings.App_ZoomLevel;
        }
    }
}
