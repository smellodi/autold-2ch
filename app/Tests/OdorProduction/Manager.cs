using System;
using System.Windows.Controls;
using AutOlD2Ch.Pages.OdorProduction;

namespace AutOlD2Ch.Tests.OdorProduction
{
    /// <summary>
    /// Manages the order of pages in the Odor Production
    /// </summary>
    public class Manager : ITestManager
    {
        public event EventHandler<PageDoneEventArgs> PageDone;

        public string Name => Utils.L10n.T("OdorPulses");

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

        public void Emulate(EmulationCommand command, params object[] args)
        {
            switch (command)
            {
                case EmulationCommand.EnableEmulation: (_setupPage as ITestEmulator).EmulationInit(); break;
                case EmulationCommand.ForceToFinishWithResult: _productionPage.Emulator.EmulationFinalize(); break;
                default: throw new NotImplementedException($"Emulation command '{command}' is not recognized in {Name}");
            }
        }

        // Internal

        readonly Setup _setupPage = new();
        readonly Production _productionPage = new();

        Page _current = null;

        Settings _settings;
    }
}
