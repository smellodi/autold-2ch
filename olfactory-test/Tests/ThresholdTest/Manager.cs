using System;
using System.Windows.Controls;
using Olfactory.Pages.ThresholdTest;
using ThreePensPage = Olfactory.Pages.ThresholdTest.ThreePens;

namespace Olfactory.Tests.ThresholdTest
{
    /// <summary>
    /// Manages the order of pages in the Threshold Test 
    /// </summary>
    public class Manager : ITestManager
    {
        public event EventHandler<bool> PageDone = delegate { };

        public string Name => Utils.L10n.T("ThresholdTest");

        public Manager()
        {
            _setupPage.Next += (s, e) =>
            {
                _settings = e;
                PageDone(this, _settings != null);
            };
            _instructionsPage.Next += (s, e) => PageDone(this, e != null);
            _familiarizePage.Next += (s, e) =>
            {
                _logger.Add(LogSource.ThTest, "familiarization", e.ToString());
                PageDone(this, true);
            };
            _threePensPage.Next += (s, e) =>
            {
                _logger.Add(LogSource.ThTest, "finished", e.ToString("F1"));

                _resultPage.SetPPM(e);
                PageDone(this, true);
            };
            _resultPage.Next += (s, e) => PageDone(this, true);
        }

        public Page NextPage()
        {
            _current = _current switch
            {
                null => _setupPage,
                Setup => _instructionsPage,
                Instructions => _familiarizePage,
                Familiarize => _threePensPage,
                ThreePensPage => _resultPage,
                Result => null,
                _ => throw new NotImplementedException($"Unhandled page in {Name}"),
            };

            if (_current != null)
            {
                _logger.Add(LogSource.ThTest, "page", _current.Title);
            }

            if (_current is Instructions instructions)
            {
                instructions.Init(_settings.Type);
            }
            if (_current is ThreePensPage threePens)
            {
                threePens.Init(_settings);
            }
            else if (_current is Familiarize famil)
            {
                famil.Init(_settings);
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
            if (_current == _familiarizePage)
            {
                _familiarizePage.Interrupt();
            }
            else if (_current == _threePensPage)
            {
                _threePensPage.Interrupt();
            }
        }

        public void Emulate(EmulationCommand command, params object[] args)
        {
            ITestEmulator emulator = _threePensPage.Emulator;

            switch (command)
            {
                case EmulationCommand.EnableEmulation: emulator.EmulationInit(); break;
                case EmulationCommand.ForceToFinishWithResult: emulator.EmulationFinilize(); break;
                case EmulationCommand.ReportKey: _threePensPage.ConsumeKeyDown((System.Windows.Input.Key)args[0]); break;
                default: throw new NotImplementedException($"Emulation command '{command}' is not recognized in {Name}");
            }
        }

        // Internal

        readonly FlowLogger _logger = FlowLogger.Instance;

        readonly Setup _setupPage = new();
        readonly Instructions _instructionsPage = new();
        readonly Familiarize _familiarizePage = new();
        readonly ThreePensPage _threePensPage = new();
        readonly Result _resultPage = new();

        Page _current = null;

        Settings _settings;
    }
}
