using System;

namespace Olfactory.Pages
{
    internal interface IPage<T>
    {
        event EventHandler<T> Next;
    }
}
