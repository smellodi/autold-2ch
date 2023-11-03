using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace AutOlD2Ch.Pages;

public partial class Finished : Page, IPage<bool>, INotifyPropertyChanged
{
    public event EventHandler<bool> Next;       // true: exit, false: return to the front page
    public event EventHandler RequestSaving;
    public event PropertyChangedEventHandler PropertyChanged;

    public string TestName
    {
        get => _testName;
        set
        {
            _testName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TestName)));
        }
    }

    public Finished()
    {
        InitializeComponent();

        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        DataContext = this;
    }

    public void DisableSaving()
    {
        btnSaveData.IsEnabled = false;
    }

    // Internal

    string _testName = "";

    // UI events

    private void Page_GotFocus(object sender, RoutedEventArgs e)
    {
        btnSaveData.IsEnabled = true;
    }

    private void SaveData_Click(object sender, RoutedEventArgs e)
    {
        RequestSaving?.Invoke(this, new EventArgs());
    }

    private void Return_Click(object sender, RoutedEventArgs e)
    {
        Next?.Invoke(this, false);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Next?.Invoke(this, true);
    }
}
