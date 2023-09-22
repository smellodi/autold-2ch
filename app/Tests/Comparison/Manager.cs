using System;
using System.Windows.Controls;
using AutOlD2Ch.Pages.Comparison;
using AutOlD2Ch.Pages.ThresholdTest;

namespace AutOlD2Ch.Tests.Comparison
{
    /// <summary>
    /// Manages the order of pages in the Comparison
    /// </summary>
    public class Manager : ITestManager
    {
        public event EventHandler<PageDoneEventArgs> PageDone;

        public string Name => Utils.L10n.T("Comparison");

        public Manager()
        {
            _setupPage.Next += (s, e) =>
            {
                _settings = e;

                if (_settings != null)
                {
                    if (_settings.Sniffer == GasSniffer.DMS)
                    {
                        _waitPage = new();
                        _waitPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));

                        _gasPresenterPage = new();
                        _gasPresenterPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));
                    }
                    else
                    {
                        _productionPracticePage = new(Stage.Practice);
                        _productionPracticePage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));

                        _productionTestPage = new(Stage.Test);
                        _productionTestPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));

                        _pausePage = new();
                        _pausePage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));

                        _vnaPage = new();
                        _vnaPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));
                    }
                }

                PageDone?.Invoke(this, new PageDoneEventArgs(_settings != null));
            };
        }

        public Page NextPage(object param = null)
        {
            var previousPage = _current;

            _current = _current switch
            {
                null => _setupPage,
                Setup _ => _settings.Sniffer switch
                {
                    GasSniffer.Human => _productionPracticePage,
                    GasSniffer.DMS => _waitPage,
                    _ => throw new NotImplementedException($"Unhandled sniffer in {Name}"),
                },
                Production prod =>
                    prod.Stage switch
                    {
                        Stage.Practice => _pausePage,
                        Stage.Test => _vnaPage,
                        _ => throw new NotImplementedException($"Unhandled production stage in {Name}"),
                    },
                Wait _ => _gasPresenterPage,
                GasPresenter _ => null,
                Pause _ => _productionTestPage,
                VnA _ => null,
                _ => throw new NotImplementedException($"Unhandled page in {Name}"),
            };

            if (_current is Production prodPage)
            {
                prodPage.Init(_settings);
            }
            else if (_current is Pause pausePage)
            {
                pausePage.Init(_settings, (previousPage as Production).Results);
            }
            else if (_current is Wait waitPage)
            {
                waitPage.Init(_settings);
            }
            else if (_current is GasPresenter gasPresenterPage)
            {
                gasPresenterPage.Init(_settings);
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
            if (_current is Production prodPage)
            {
                prodPage.Interrupt();
            }
        }

        public void Emulate(EmulationCommand command, params object[] args)
        {
            switch (command)
            {
                case EmulationCommand.EnableEmulation: (_setupPage as ITestEmulator).EmulationInit(); break;
                case EmulationCommand.ForceToFinishWithResult: if (_current is Production prodPage) prodPage.Emulator.EmulationFinilize(); break;
                default: throw new NotImplementedException($"Emulation command '{command}' is not recognized in {Name}");
            }
        }

        // Internal

        readonly Setup _setupPage = new();
        Wait _waitPage;
        GasPresenter _gasPresenterPage;
        Production _productionPracticePage;
        Production _productionTestPage;
        Pause _pausePage;
        VnA _vnaPage;

        Page _current = null;

        Settings _settings;
    }
}
