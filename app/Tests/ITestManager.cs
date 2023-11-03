using System;
using System.Windows.Controls;

namespace AutOlD2Ch.Tests
{
    public enum Test
    {
        OdorProduction,
        Comparison,
        LptController,
    }

    public class PageDoneEventArgs : EventArgs
    {
        public bool CanContinue { get; private set; }
        public object Data { get; private set; }
        public PageDoneEventArgs(bool canContinue, object data = null)
        {
            CanContinue = canContinue;
            Data = data;
        }
    }

    public interface ITestManager
    {
        event EventHandler<PageDoneEventArgs> PageDone;
        string Name { get; }
        Page Start();
        Page NextPage(object param);
        void Interrupt();
        void Emulate(EmulationCommand command, params object[] args);
    }

    public enum EmulationCommand
    {
        EnableEmulation,
        ForceToFinishWithResult,
    }
}
