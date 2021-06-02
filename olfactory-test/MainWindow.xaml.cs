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

            _setupPage.LogResult += (s, comResult) => _monitor.LogResult(comResult.Source, comResult.Result);
            _setupPage.Next += (s, test) =>
            {
                _currentTest = test switch
                {
                    Tests.Test.Threshold => new Tests.ThresholdTest.Manager(),
                    Tests.Test.OdorProduction => new Tests.OdorProduction.Manager(),
                    _ => throw new NotImplementedException($"The test '{test}' logic is not implemented yet"),
                };

                _currentTest.PageDone += (s, e) => Continue();

                Content = _currentTest.Start();

                if (Comm.MFC.Instance.IsDebugging)
                {
                    _currentTest.Emulate(Tests.EmulationCommand.EnableEmulation);
                }
            };

            _finishedPage.Next += (s, exit) =>
            {
                if (exit)
                {
                    Close();
                }
                else
                {
                    Content = _setupPage;
                }
            };
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
                logger.SaveTo($"olfactory_{DateTime.Now:u}.txt".ToPath());
            }

            SyncLogger syncLogger = SyncLogger.Instance;
            syncLogger.Finilize();
            if (syncLogger.HasRecords)
            {
                syncLogger.SaveTo($"olfactory_sync_{DateTime.Now:u}.txt".ToPath());
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

            Application.Current.Shutdown();
        }
    }
}
