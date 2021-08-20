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
            _finishedPage.RequestSaving += OnFinishedPage_RequestSaving;

            var settings = Properties.Settings.Default;
            if (settings.MainWindow_Width > 0)
            {
                Left = settings.MainWindow_X;
                Top = settings.MainWindow_Y;
                Width = settings.MainWindow_Width;
                Height = settings.MainWindow_Height;
            }
        }

        private bool SaveLoggingData()
        {
            bool hasData = false;

            FlowLogger logger = FlowLogger.Instance;
            if (logger.HasTestRecords)
            {
                hasData = true;
                if (logger.SaveTo($"olfactory_{DateTime.Now:u}.txt".ToPath()))
                {
                    _finishedPage.DisableSaving();
                }
            }

            SyncLogger syncLogger = SyncLogger.Instance;
            syncLogger.Finilize();
            if (syncLogger.HasRecords)
            {
                hasData = true;
                if (syncLogger.SaveTo($"olfactory_sync_{DateTime.Now:u}.txt".ToPath()))
                {
                    _finishedPage.DisableSaving();
                }
            }

            return hasData;
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
            FlowLogger.Instance.Clear();
            SyncLogger.Instance.Clear();

            if (exit)
            {
                Close();
            }
            else
            {
                Content = _setupPage;
            }
        }

        private void OnFinishedPage_RequestSaving(object sender, EventArgs e)
        {
            if (!SaveLoggingData())
            {
                _finishedPage.DisableSaving();
                MessageBox.Show($"There is no data to save", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnTest_PageDone(object sender, EventArgs args)
        {
            var page = _currentTest.NextPage();
            if (page == null)
            {
                _finishedPage.TestName = _currentTest.Name;
                Content = _finishedPage;
                _currentTest = null;
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
            else
            {
                _currentTest?.Emulate(Tests.EmulationCommand.ReportKey, e.Key);
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
            if (_currentTest != null)
            {
                _currentTest.Interrupt();
                SaveLoggingData();
            }

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
