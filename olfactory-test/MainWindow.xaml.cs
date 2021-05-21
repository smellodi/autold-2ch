using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Utils;

namespace Olfactory
{
    public partial class MainWindow : Window
    {
        Pages.Setup _setupPage = new Pages.Setup();
        Pages.Finished _finishedPage = new Pages.Finished();

        CommMonitor _monitor = new CommMonitor();
        Tests.ITest _currentTest = null;

        public MainWindow()
        {
            InitializeComponent();

            _monitor.Hide();

            _setupPage.LogResult += (s, e) => _monitor.LogResult(e.Source, e.Result);
            _setupPage.Next += (s, e) =>
            {
                _currentTest = e switch
                {
                    Tests.Test.Threshold => new Tests.ThresholdTest(),
                    _ => throw new NotImplementedException($"The test '{e}' logic is not implemented yet"),
                };

                _currentTest.PageDone += (s, e) => Continue();

                Content = _currentTest.Start();

                if (MFC.Instance.IsDebugging)
                {
                    _currentTest.Emulate(Tests.EmulationCommand.EnableEmulation);
                }
            };

            _finishedPage.Next += (s, e) => Close();

            Content = _setupPage;
        }

        private void Continue()
        {
            var page = _currentTest.NextPage();
            Content = page != null ? page : _finishedPage;
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Logger logger = Logger.Instance;
            if (logger.HasAnyRecord)
            {
                logger.SaveTo($"olfactory_log_{DateTime.Now:u}.txt".ToPath());
            }

            Application.Current.Shutdown();
        }
    }
}
