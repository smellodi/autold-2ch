using System;
using System.Windows.Controls;

namespace Olfactory.Tests
{
    public enum Test
    {
        Threshold,
        OdorProduction,
    }

    public interface ITestManager
    {
        event EventHandler<bool> PageDone;  // bool: 'True' to continue, 'False' to quit
        string Name { get; }
        Page Start();
        Page NextPage();
        void Interrupt();
        void Emulate(EmulationCommand command, params object[] args);
    }

    public enum EmulationCommand
    {
        EnableEmulation,
        ForceToFinishWithResult,
        ReportKey,
    }
}
