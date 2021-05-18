using System;
using System.Collections.Generic;
using System.Text;

namespace Olfactory.Pages
{
    internal interface IPage<T>
    {
        event EventHandler<T> Next;
    }
}
