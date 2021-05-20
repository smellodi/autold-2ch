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
    }
}
