using System;
using System.Windows.Controls;
using Olfactory2Ch.Pages.Comparison;
using Olfactory2Ch.Pages.ThresholdTest;

namespace Olfactory2Ch.Tests.Comparison
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
                PageDone?.Invoke(this, new PageDoneEventArgs(_settings != null));
            };
            _productionPracticePage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));
            _productionTestPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));
            _pausePage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));
            _vnaPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));
        }

        public Page NextPage(object param = null)
        {
            var previousPage = _current;

            _current = _current switch
            {
                null => _setupPage,
                Setup _ => _productionPracticePage,
                Production prod =>
                    prod.Stage switch {
                        Stage.Practice => _pausePage,
                        Stage.Test => _vnaPage,
                        _ => throw new NotImplementedException($"Unhandled production stage in {Name}"),
                    },
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
        readonly Production _productionPracticePage = new(Stage.Practice);
        readonly Production _productionTestPage = new(Stage.Test);
        readonly Pause _pausePage = new();
        readonly VnA _vnaPage = new();

        Page _current = null;

        Settings _settings;
    }
}
