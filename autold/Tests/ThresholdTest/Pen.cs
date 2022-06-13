using System.Windows.Controls;

namespace Olfactory.Tests.ThresholdTest
{
    public enum PenColor { None, Odor, NonOdor }

    public class Pen
    {
        public PenColor Color { get; set; }

        public Pen(PenColor color = PenColor.None)
        {
            Color = color;
        }
    }
}
