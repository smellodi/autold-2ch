using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutOlD2Ch.Pages.ThresholdTest;

public partial class VnA : Page, IPage<EventArgs>
{
    public event EventHandler<EventArgs>? Next;

    public static string Header => "VnA";

    public Dictionary<string, string> Entries => new()
    {
        { "Valence", sclQ1.Value?.ToString() ?? "" },
        { "Arousal", sclQ2.Value?.ToString() ?? "" },
    };

    public VnA()
    {
        InitializeComponent();

        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        if (Focusable)
        {
            Focus();
        }
    }

    // Internal

    private void UpdateUI()
    {
        btnNext.IsEnabled =
            sclQ1.Value != null &&
            sclQ2.Value != null;
    }

    private void ScaleValue_Changed(object sender, RoutedEventArgs e)
    {
        UpdateUI();
    }
    
    // UI

    private void Page_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            sclQ1.Value = 2;
            sclQ2.Value = 2;

            UpdateUI();
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        var _eventLogger = FlowLogger.Instance;

        foreach (var entry in Entries)
        {
            _eventLogger.Add(LogSource.Comparison, Header, entry.Key, entry.Value);
        }

        Next?.Invoke(this, new EventArgs());
    }
}
