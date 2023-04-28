using System;

namespace Olfactory2Ch.Pages
{
    internal interface IPage<T>
    {
        event EventHandler<T> Next;
    }
}
