using Olfactory.Utils;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using WPFLocalizeExtension.Engine;

namespace Olfactory
{
    public partial class MainWindow : Window
    {
        readonly Pages.Setup _setupPage = new();
        readonly Pages.Finished _finishedPage = new();

        readonly CommMonitor _monitor;

        Tests.ITestManager _currentTest = null;

        readonly Storage _storage = Storage.Instance;

        public MainWindow()
        {
            InitializeComponent();

            LocalizeDictionary.Instance.MergedAvailableCultures.RemoveAt(0);
            LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo(Properties.Settings.Default.Language);
            LocalizeDictionary.Instance.OutputMissingKeys = true;
            LocalizeDictionary.Instance.MissingKeyEvent += (s, e) => e.MissingKeyResult = $"[MISSING] {e.Key}";

            Title += $" [build {Properties.Resources.BuildCode.Trim()}]";

            _monitor = new CommMonitor();
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

        private SavingResult? SaveLoggingData()
        {
            SavingResult? result = null;

            FlowLogger flowLogger = FlowLogger.Instance;
            if (flowLogger.HasTestRecords)
            {
                var savingResult = flowLogger.SaveTo($"olfactory_{DateTime.Now:u}.txt".ToPath());
                if (savingResult != SavingResult.Cancel)
                {
                    _finishedPage.DisableSaving();
                }

                result = savingResult;
            }

            SyncLogger syncLogger = SyncLogger.Instance;
            syncLogger.Finilize();
            if (syncLogger.HasRecords)
            {
                var savingResult = syncLogger.SaveTo($"olfactory_{DateTime.Now:u}.txt".ToPath());
                if (savingResult != SavingResult.Cancel)
                {
                    _finishedPage.DisableSaving();
                }

                result = savingResult;
            }

            return result;
        }

        private void OnSetupPage_Next(object sender, Tests.Test test)
        {
            _currentTest = test switch
            {
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
                FlowLogger.Instance.Clear();
                SyncLogger.Instance.Clear();

                Content = _setupPage;
            }
        }

        private void OnFinishedPage_RequestSaving(object sender, EventArgs e)
        {
            var savingResult = SaveLoggingData();
            if (savingResult == null)
            {
                _finishedPage.DisableSaving();
                MsgBox.Warn(Title, L10n.T("NoDataToSave"), MsgBox.Button.OK);
            }
        }

        private void OnTest_PageDone(object sender, Tests.PageDoneEventArgs e)
        {
            if (!e.CanContinue)
            {
                _currentTest.Interrupt();
                _currentTest = null;
                Content = _setupPage;

                SyncLogger.Instance.Finilize();
                SyncLogger.Instance.Clear();
                FlowLogger.Instance.Clear();
            }
            else
            {
                var page = _currentTest.NextPage(e.Data);
                if (page == null)
                {
                    _finishedPage.TestName = _currentTest.Name;
                    Content = _finishedPage;
                    _currentTest = null;
                    DispatchOnceUI.Do(0.1, () => SaveLoggingData());  // let the page to change, then try to save data
                }
                else
                {
                    Content = page;
                }
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
            if (_currentTest != null)
            {
                _currentTest.Interrupt();
            }

            e.Cancel = SaveLoggingData() == SavingResult.Cancel;

            if (!e.Cancel)
            {
                _storage.Dispose();

                var settings = Properties.Settings.Default;
                settings.MainWindow_X = Left;
                settings.MainWindow_Y = Top;
                settings.MainWindow_Width = Width;
                settings.MainWindow_Height = Height;
                settings.Language = LocalizeDictionary.Instance.Culture.Name;
                settings.Save();

                Application.Current.Shutdown();
            }
        }
    }
}
