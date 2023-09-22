using System;

namespace AutOlD2Ch.Pages
{
    internal interface IPage<T>
    {
        event EventHandler<T> Next;
    }
}
