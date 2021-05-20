namespace Olfactory.Tests
{
    public partial class ThresholdTest : ITest
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
}
