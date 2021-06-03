using Olfactory.Utils;
using System;
using System.Windows;
using System.Windows.Input;

namespace Olfactory
{
    public partial class MainWindow : Window
    {
        Pages.Setup _setupPage = new Pages.Setup();
        Pages.Finished _finishedPage = new Pages.Finished();

        CommMonitor _monitor = new CommMonitor();
        Tests.ITestManager _currentTest = null;

        Storage _storage = Storage.Instance;

        public MainWindow()
        {
            InitializeComponent();

            _monitor.Hide();

            _setupPage.Next += OnSetupPage_Next;
            _finishedPage.Next += OnFinishedPage_Next;

            var settings = Properties.Settings.Default;
            if (settings.MainWindow_Width > 0)
            {
                Left = settings.MainWindow_X;
                Top = settings.MainWindow_Y;
                Width = settings.MainWindow_Width;
                Height = settings.MainWindow_Height;
            }
        }

        private void SaveLoggingData()
        {
            FlowLogger logger = FlowLogger.Instance;
            if (logger.HasTestRecords)
            {
                logger.SaveTo($"olfactory_{DateTime.Now:u}.txt".ToPath());
            }

            SyncLogger syncLogger = SyncLogger.Instance;
            syncLogger.Finilize();
            if (syncLogger.HasRecords)
            {
                syncLogger.SaveTo($"olfactory_sync_{DateTime.Now:u}.txt".ToPath());
            }
        }

        private void OnSetupPage_Next(object sender, Tests.Test test)
        {
            _currentTest = test switch
            {
                Tests.Test.Threshold => new Tests.ThresholdTest.Manager(),
                Tests.Test.OdorProduction => new Tests.OdorProduction.Manager(),
                _ => throw new NotImplementedException($"The test '{test}' logic is not implemented yet"),
            };

            _currentTest.PageDone += OnTest_PageDone;

            Content = _currentTest.Start();

            if (_storage.IsDebugging)
            {
                _currentTest.Emulate(Tests.EmulationCommand.EnableEmulation);
            }
        }

        private void OnFinishedPage_Next(object sender, bool exit)
        {
            if (exit)
            {
                Close();
            }
            else
            {
                Content = _setupPage;
            }
        }

        private void OnTest_PageDone(object sender, EventArgs args)
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


        // UI events


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Delta > 0)
                {
                    _storage.ZoomIn();
                }
                else
                {
                    _storage.ZoomOut();
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveLoggingData();

            _storage.Dispose();

            var settings = Properties.Settings.Default;
            settings.MainWindow_X = Left;
            settings.MainWindow_Y = Top;
            settings.MainWindow_Width = Width;
            settings.MainWindow_Height = Height;
            settings.Save();

            Application.Current.Shutdown();
        }
    }
}
