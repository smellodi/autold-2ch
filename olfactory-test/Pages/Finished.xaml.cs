﻿using System;
using System.Windows;
using System.Windows.Controls;

namespace Olfactory.Pages
{
    public partial class Finished : Page, IPage<bool>
    {
        public event EventHandler<bool> Next = delegate { }; // true: exit, false: return to the fornt page
        public event EventHandler RequestSaving = delegate { };

        public Finished()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);
        }

        public void DisableSaving()
        {
            btnSaveData.IsEnabled = false;
        }

        // UI events

        private void Page_GotFocus(object sender, RoutedEventArgs e)
        {
            btnSaveData.IsEnabled = true;
        }

        private void OnSaveData_Click(object sender, RoutedEventArgs e)
        {
            RequestSaving(this, new EventArgs());
        }

        private void OnReturn_Click(object sender, RoutedEventArgs e)
        {
            Next(this, false);
        }

        private void OnExit_Click(object sender, RoutedEventArgs e)
        {
            Next(this, true);
        }
    }
}
