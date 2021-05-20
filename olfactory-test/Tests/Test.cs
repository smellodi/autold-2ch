using System;
using System.Windows.Controls;

namespace Olfactory.Tests
{
    public enum Test
    {
        Threshold
    }

    public interface ITest
    {
        event EventHandler PageDone;
        Page Start();
        Page NextPage();
        void Emulate(EmulationCommand command, params object[] args);
    }

    public enum EmulationCommand
    {
        EnableEmulation,
        ForceToFinishWithResult,
    }
}
