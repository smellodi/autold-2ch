﻿using System;
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

namespace Olfactory
{
    public partial class MainWindow : Window
    {
        Pages.Setup _setupPage = new Pages.Setup();
        Pages.Finished _finishedPage = new Pages.Finished();

        CommMonitor _debug = new CommMonitor();
        Test.ITest _currentTest = null;

        public MainWindow()
        {
            InitializeComponent();

            _debug.Hide();

            _setupPage.LogResult += (s, e) => _debug.LogResult(s as string, e);
            _setupPage.Next += (s, e) =>
            {
                _currentTest = e switch
                {
                    Test.Tests.Threshold => new Test.ThresholdTest(),
                    _ => throw new NotImplementedException($"The test '{e}' logic is not implemented yet"),
                };

                _currentTest.Continue += (s, e) => Continue();

                Content = _currentTest.NextPage();
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
                _debug.Show();
            }
            /*else if (e.Key == Key.Space)
            {
                if (_currentTest != null)
                {
                    Continue();
                }
            }*/
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
