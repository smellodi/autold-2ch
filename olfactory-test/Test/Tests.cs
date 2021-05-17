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
        event EventHandler Continue;
        Page NextPage();
    }
}
