using System;
using System.Windows.Controls;

namespace Olfactory.Tests
{
    /// <summary>
    /// Manages the order of pages in the Threshold Test 
    /// </summary>
    public partial class ThresholdTest : ITest
    {
        public event EventHandler PageDone = delegate { };

        public ThresholdTest()
        {
            _instructionsPage.Next += (s, e) => PageDone(this, new EventArgs());
            _familiarizePage.Next += (s, e) =>
            {
                _logger.Add(LogSource.ThTest, "familiarization", e.ToString());
                PageDone(this, new EventArgs());
            };
            _threePensPage.Next += (s, e) =>
            {
                _logger.Add(LogSource.ThTest, "result", e.ToString());
                _threePensPage.Init();
                _logger.Add(LogSource.ThTest, "trial", _threePensPage.Procedure.State);
            };
            _threePensPage.Finished += (s, e) =>
            {
                _logger.Add(LogSource.ThTest, "result", true.ToString());
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
                Pages.ThresholdTest.Instructions _ => _familiarizePage,
                Pages.ThresholdTest.Familiarize _ => _threePensPage,
                Pages.ThresholdTest.ThreePens _ => _resultPage,
                Pages.ThresholdTest.Result _ => null,
                _ => throw new NotImplementedException("Unhandled page in the Threhold Test"),
            };

            if (_current != null)
            {
                _logger.Add(LogSource.ThTest, "page", _current.Title);
            }

            if (_current is Pages.ThresholdTest.ThreePens page)
            {
                page.Init();
                _logger.Add(LogSource.ThTest, "trial", _threePensPage.Procedure.State);
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

        Pages.ThresholdTest.Instructions _instructionsPage = new Pages.ThresholdTest.Instructions();
        Pages.ThresholdTest.Familiarize _familiarizePage = new Pages.ThresholdTest.Familiarize();
        Pages.ThresholdTest.ThreePens _threePensPage = new Pages.ThresholdTest.ThreePens();
        Pages.ThresholdTest.Result _resultPage = new Pages.ThresholdTest.Result();

        Page _current = null;

        Logger _logger = Logger.Instance;
    }
}
