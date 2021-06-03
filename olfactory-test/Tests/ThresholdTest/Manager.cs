using System;
using System.Windows.Controls;
using Olfactory.Pages.ThresholdTest;

namespace Olfactory.Tests.ThresholdTest
{
    /// <summary>
    /// Manages the order of pages in the Threshold Test 
    /// </summary>
    public class Manager : ITestManager
    {
        public event EventHandler PageDone = delegate { };

        public Manager()
        {
            _instructionsPage.Next += (s, e) => PageDone(this, new EventArgs());
            _familiarizePage.Next += (s, e) =>
            {
                _logger.Add(LogSource.ThTest, "familiarization", e.ToString());
                PageDone(this, new EventArgs());
            };
            _threePensPage.Next += (s, e) =>
            {
                _logger.Add(LogSource.ThTest, "finished", e.ToString("F1"));

                _resultPage.SetPPM(e);
                PageDone(this, new EventArgs());
            };
            _resultPage.Next += (s, e) => PageDone(this, new EventArgs());
        }

        public Page NextPage()
        {
            _current = _current switch
            {
                null => _instructionsPage,
                Instructions _ => _familiarizePage,
                Familiarize _ => _threePensPage,
                ThreePens _ => _resultPage,
                Result _ => null,
                _ => throw new NotImplementedException("Unhandled page in the Threhold Test"),
            };

            if (_current != null)
            {
                _logger.Add(LogSource.ThTest, "page", _current.Title);
            }

            if (_current is ThreePens page)
            {
                page.Init();
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
            ITestEmulator emulator = _threePensPage.Emulator;

            switch (command)
            {
                case EmulationCommand.EnableEmulation: emulator.EmulationInit(); break;
                case EmulationCommand.ForceToFinishWithResult: emulator.EmulationFinilize(); break;
                default: throw new NotImplementedException("This emulation command is not recognized in Threshold Test");
            }
        }

        // Internal

        Instructions _instructionsPage = new Instructions();
        Familiarize _familiarizePage = new Familiarize();
        ThreePens _threePensPage = new ThreePens();
        Result _resultPage = new Result();

        Page _current = null;

        FlowLogger _logger = FlowLogger.Instance;
    }
}
