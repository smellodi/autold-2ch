using System;
using System.Windows.Controls;
using Olfactory.Pages.ThresholdTest;
using PenPresentation = Olfactory.Pages.ThresholdTest.PenPresentation;

namespace Olfactory.Tests.ThresholdTest
{
    public enum Navigation
    {
        Familiarization,
        Back,
        Practice,
        Test,
    }

    /// <summary>
    /// Manages the order of pages in the Threshold Test 
    /// </summary>
    public class Manager : ITestManager
    {
        public event EventHandler<PageDoneEventArgs> PageDone;

        public string Name => Utils.L10n.T("ThresholdTest");

        public Manager()
        {
            _setupPage.Next += (s, e) =>
            {
                _settings = e;
                PageDone?.Invoke(this, new PageDoneEventArgs(_settings != null));
            };
            _instructionsPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(e != Navigation.Back, e));
            _practicingPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true, Navigation.Back));
            _familiarizePage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true, e));
            _threePensPage.Next += (s, e) =>
            {
                _logger.Add(LogSource.ThTest, "finished", e.ToString("F1"));

                _resultPage.SetPPM(e);
                PageDone?.Invoke(this, new PageDoneEventArgs(true));
            };
            _resultPage.Next += (s, e) => PageDone?.Invoke(this, new PageDoneEventArgs(true));
        }

        public Page NextPage(object param = null)
        {
            _current = _current switch
            {
                null => _setupPage,
                Setup => _instructionsPage,
                Instructions => (Navigation?)param switch
                {
                    Navigation.Familiarization => _familiarizePage,
                    Navigation.Practice => _practicingPage,
                    Navigation.Test => _settings.Type switch
                    {
                        Settings.ProcedureType.ThreePens => _threePensPage,
                        Settings.ProcedureType.TwoPens => _threePensPage,
                        Settings.ProcedureType.OnePen => _threePensPage,
                        _ => throw new NotImplementedException($"Unhandled procedure type in {Name}"),
                    },
                    _ => throw new NotImplementedException($"Unhandled instruction navigation type in {Name}"),
                },
                Familiarize => (Navigation?)param switch
                {
                    Navigation.Back => _instructionsPage,
                    _ => _settings.Type switch {
                        Settings.ProcedureType.ThreePens => _threePensPage,
                        Settings.ProcedureType.TwoPens => _threePensPage,
                        Settings.ProcedureType.OnePen => _threePensPage,
                        _ => throw new NotImplementedException($"Unhandled procedure type in {Name}"),
                    },
                },
                PenPresentation => (Navigation?)param switch
                {
                    Navigation.Back => _instructionsPage,
                    null => _resultPage,
                    _ => throw new NotImplementedException($"Unhandled pen proc type navigation type in {Name}"),
                },
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
            else if (_current is Familiarize famil)
            {
                famil.Init(_settings);
            }
            else if (_current is PenPresentation threePens)
            {
                threePens.Start(_settings);

                if (threePens == _practicingPage)
                {
                    threePens.Emulator.EmulationFinilize();
                }
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
        readonly PenPresentation _practicingPage = new(true);
        readonly PenPresentation _threePensPage = new(false);
        readonly Result _resultPage = new();

        Page _current = null;

        Settings _settings;
    }
}
