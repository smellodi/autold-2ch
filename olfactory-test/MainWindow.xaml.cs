using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Olfactory.Utils;

namespace Olfactory
{
    public partial class MainWindow : Window
    {
        Pages.Setup _setupPage = new Pages.Setup();
        Pages.Finished _finishedPage = new Pages.Finished();

        CommMonitor _monitor = new CommMonitor();
        Tests.ITestManager _currentTest = null;

        public MainWindow()
        {
            InitializeComponent();

            _monitor.Hide();

            _setupPage.LogResult += (s, e) => _monitor.LogResult(e.Source, e.Result);
            _setupPage.Next += (s, e) =>
            {
                _currentTest = e switch
                {
                    Tests.Test.Threshold => new Tests.ThresholdTest.Manager(),
                    Tests.Test.OdorProduction => new Tests.OdorProduction.Manager(),
                    _ => throw new NotImplementedException($"The test '{e}' logic is not implemented yet"),
                };

                _currentTest.PageDone += (s, e) => Continue();

                Content = _currentTest.Start();

                if (Comm.MFC.Instance.IsDebugging)
                {
                    _currentTest.Emulate(Tests.EmulationCommand.EnableEmulation);
                }
            };

            _finishedPage.Next += (s, e) => Close();
        }

        private void Continue()
        {
            var page = _currentTest.NextPage();
            if (page == null)
            {
                Content = _finishedPage;
                DispatchOnce.Do(0.3, () => SaveLoggingData());  // let the page to change, then try to save data
            }
            else
            {
                Content = page;
            }
        }

        private void SaveLoggingData()
        {
            Logger logger = Logger.Instance;
            if (logger.HasTestRecords)
            {
                logger.SaveTo($"olfactory_log_{DateTime.Now:u}.txt".ToPath());
            }
        }


        // UI events


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            /* Unfortunatelly, Page cannot be a child of Viewbox, thus zooming functionality can be implemented on individual pages only
            SizeToContent = SizeToContent.WidthAndHeight;

            View.Width = Width;
            View.Height = Height;

            View.Child = _setupPage;

            */
            Content = _setupPage;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                _monitor.Show();
            }
            else if (e.Key == Key.F9)
            {
                _currentTest?.Emulate(Tests.EmulationCommand.ForceToFinishWithResult);
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            /*
            const int ZOOM_RATE = 20;
            var delta = e.Delta > 0 ? ZOOM_RATE : Math.Max(-ZOOM_RATE, -Math.Min(View.ActualWidth, View.ActualHeight));

            View.Width += delta;
            View.Height += delta;
            */
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveLoggingData();

            Application.Current.Shutdown();
        }
    }
}
