using System;
using System.Windows.Controls;
using Olfactory.Pages.OdorProduction;

namespace Olfactory.Tests.OdorProduction
{
    /// <summary>
    /// Manages the order of pages in the Threshold Test 
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
            _productionPage.Next += (s, e) => _productionPage.Run(e);
            _productionPage.Finished += (s, e) => PageDone(this, new EventArgs());
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

            if (_current != null)
            {
                _logger.Add(LogSource.OdProd, "page", _current.Title);
            }

            if (_current is Production page)
            {
                page.Init(_setting);
                page.Run(0);
            }

            return _current;
        }

        public Page Start()
        {
            _current = null;
            return NextPage();
        }

        public void Emulate(EmulationCommand command, params object[] args)
        {/* TODO
            ITestEmulator emulator = _productionPage.Emulator;

            switch (command)
            {
                case EmulationCommand.EnableEmulation: emulator.EmulationInit(); break;
                case EmulationCommand.ForceToFinishWithResult: emulator.EmulationFinilize(); break;
                default: throw new NotImplementedException("This emulation command is not recognized in Odor Production");
            }*/
        }

        // Internal

        Setup _setupPage = new Setup();
        Production _productionPage = new Production();

        Page _current = null;

        Settings _setting;

        Logger _logger = Logger.Instance;
    }
}
