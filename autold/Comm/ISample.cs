﻿namespace Olfactory.Comm
{
    public interface ISample
    {
        public long Time { get; }
        public double MainValue { get; }
    }
}