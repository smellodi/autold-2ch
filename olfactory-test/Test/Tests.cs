using System;
using System.Windows.Controls;

namespace Olfactory.Test
{
    public enum Tests
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
