namespace Olfactory.Tests.ThresholdTest
{
    public enum PenColor { None, Red, Green, Blue }

    public class Pen
    {
        public PenColor Color { get; set; } = PenColor.None;

        public Pen(PenColor color = PenColor.None)
        {
            Color = color;
        }
    }
}
