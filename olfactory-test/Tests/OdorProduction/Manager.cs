using System;
using System.Windows.Controls;
using Olfactory.Pages.OdorProduction;

namespace Olfactory.Tests.OdorProduction
{
    /// <summary>
    /// Manages the order of pages in the Odor Production
    /// </summary>
    public class Manager : ITestManager
    {
        public event EventHandler PageDone = delegate { };

        public Manager()
        {
            _setupPage.Next += (s, e) =>
            {
                _setting = e;
                PageDone(this, new EventArgs());
            };
            _productionPage.Next += (s, e) => PageDone(this, new EventArgs());
        }

        public Page NextPage()
        {
            _current = _current switch
            {
                null => _setupPage,
                Setup _ => _productionPage,
                Production _ => null,
                _ => throw new NotImplementedException("Unhandled page in Odor Production"),
            };

            if (_current is Production page)
            {
                page.Init(_setting);
            }

            return _current;
        }

        public Page Start()
        {
            _current = null;
            return NextPage();
        }

        public void Emulate(EmulationCommand command, params object[] args)
        {
            switch (command)
            {
                case EmulationCommand.EnableEmulation: (_setupPage as ITestEmulator).EmulationInit(); break;
                case EmulationCommand.ForceToFinishWithResult: _productionPage.Emulator.EmulationFinilize(); break;
                default: throw new NotImplementedException("This emulation command is not recognized in Odor Production");
            }
        }

        // Internal

        Setup _setupPage = new Setup();
        Production _productionPage = new Production();

        Page _current = null;

        Settings _setting;
    }
}
