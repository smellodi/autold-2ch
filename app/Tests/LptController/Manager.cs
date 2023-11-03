using System;
using System.Windows.Controls;
using AutOlD2Ch.Pages.LptController;

namespace AutOlD2Ch.Tests.LptController;

/// <summary>
/// Manages the order of pages in the LPT Controller
/// </summary>
public class Manager : ITestManager
{
    public event EventHandler<PageDoneEventArgs> PageDone;

    public string Name => Utils.L10n.T("LptController");

    public Manager()
    {
        _setupPage.Next += (s, e) =>
        {
            _settings = e;
            PageDone?.Invoke(this, new PageDoneEventArgs(_settings != null));
        };
        _productionPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));
    }

    public Page NextPage(object param = null)
    {
        _current = _current switch
        {
            null => _setupPage,
            Setup _ => _productionPage,
            Production _ => null,
            _ => throw new NotImplementedException($"Unhandled page in {Name}"),
        };

        if (_current is Production page)
        {
            page.Init(_settings);
        }

        return _current;
    }

    public Page Start()
    {
        _current = null;
        return NextPage();
    }

    public void Interrupt()
    {
        if (_current == _productionPage)
        {
            _productionPage.Interrupt();
        }
    }

    public void Emulate(EmulationCommand command, params object[] args) { }

    // Internal

    readonly Setup _setupPage = new();
    readonly Production _productionPage = new();

    Page _current = null;

    Settings _settings;
}
