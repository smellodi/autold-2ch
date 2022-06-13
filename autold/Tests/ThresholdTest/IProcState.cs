namespace Olfactory.Tests.ThresholdTest
{
    public interface IProcState
    {
        public enum PPMChangeDirection { Increasing, Decreasing }

        int Step { get; }
        PPMChangeDirection Direction { get; }
        double PPM { get; }
        int RecognitionsInRow { get; }
        int TurningPointCount { get; }
    }
}
