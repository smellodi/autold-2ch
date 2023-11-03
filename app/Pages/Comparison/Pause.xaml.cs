using AutOlD2Ch.Tests.Comparison;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace AutOlD2Ch.Pages.ThresholdTest;

public partial class Pause : Page, IPage<EventArgs>
{
    public event EventHandler<EventArgs>? Next;

    public Pause()
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

    public void Init(Settings settings, (MixturePair, Procedure.Answer)[] results)
    {
        var margin = new Thickness(0, 2, 0, 2);
        var padding = new Thickness(32, 6, 32, 6);
        var colorCorrect = new SolidColorBrush(Color.FromRgb(0xc0, 0xff, 0xc8));
        var colorWrong = new SolidColorBrush(Color.FromRgb(0xff, 0xc8, 0xc0));

        foreach (var result in results)
        {
            var pair = result.Item1;
            var correctAnswer = pair.Mix1 == pair.Mix2 ? Procedure.Answer.Same : Procedure.Answer.Different;
            var gas1 = pair.Mix1 switch
            {
                Mixture.Odor1 => settings.Gas1.ToString(),
                Mixture.Odor2 => settings.Gas2.ToString(),
                _ => pair.Mix1.ToString()
            };
            var gas2 = pair.Mix2 switch
            {
                Mixture.Odor1 => settings.Gas1.ToString(),
                Mixture.Odor2 => settings.Gas2.ToString(),
                _ => pair.Mix2.ToString()
            };
            var lblLabel = new Label()
            {
                Content = $"{gas1} - {gas2}",
                Padding = padding,
                Margin = margin,
            };
            var lblMark = new Label()
            {
                Content = result.Item2 == correctAnswer ? "correct" : "wrong",
                Padding = padding,
                Margin = margin,
                MinWidth = 120,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = result.Item2 == correctAnswer ? colorCorrect : colorWrong
            };
            var wrp = new WrapPanel()
            {
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            wrp.Children.Add(lblLabel);
            wrp.Children.Add(lblMark);
            stpResults.Children.Add(wrp);
        }
    }

    // Internal

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        Next?.Invoke(this, new EventArgs());
    }
}
