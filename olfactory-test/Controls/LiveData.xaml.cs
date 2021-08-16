using System.Collections.Generic;
using System.Windows.Controls;

namespace Olfactory.Controls
{
    public partial class LiveData : UserControl
    {
        static readonly System.Drawing.Color LINE_COLOR = System.Drawing.Color.FromArgb(16, 160, 255);

        public class MeasureModel
        {
            public double Timestamp { get; set; }
            public double Value { get; set; }
        }

        public LiveData()
        {
            InitializeComponent();

            _scatter = new ScottPlot.Plottable.ScatterPlot(new double[] { 0 }, new double[] { 0 });
            _scatter.Color = LINE_COLOR;
            _scatter.MarkerSize = 4f;

            chart.Plot.Add(_scatter);
            chart.Plot.XAxis.Color(COLOR_GRAY);
            chart.Plot.YAxis.Color(COLOR_GRAY);

            chart.Plot.XAxis.TickLabelStyle(fontSize: 10);
            chart.Plot.XAxis.SetSizeLimit(10, 20, 0);
            chart.Plot.XAxis.Line(false);
            chart.Plot.XAxis.Ticks(true, false, true);
            chart.Plot.YAxis.TickLabelStyle(fontSize: 10);
            chart.Plot.YAxis.SetSizeLimit(10, 30, 0);
            chart.Plot.YAxis.Line(false);

            chart.Plot.XAxis2.Hide();
            chart.Plot.YAxis2.Hide();
        }

        public void Reset(double baseline = 0)
        {
            _data.Clear();

            var count = ActualWidth / PIXELS_PER_POINT;
            var ts = Utils.Timestamp.Sec;

            while (_data.Count < count)
            {
                _data.Add(new MeasureModel
                {
                    Timestamp = ts + _data.Count - count,
                    Value = baseline,
                });
            }

            Data2XY(out double[] x, out double[] y);

            _scatter.Update(x, y);
            chart.Plot.AxisAuto();
            chart.Render();
        }

        public void Add(double timestamp, double value, System.Drawing.Color? brush = null)
        {
            while (_data.Count > ActualWidth / PIXELS_PER_POINT)
            {
                _data.RemoveAt(0);
            }

            _data.Add(new MeasureModel { Timestamp = timestamp, Value = value });

            Data2XY(out double[] x, out double[] y);

            _scatter.Update(x, y);
            chart.Plot.AxisAuto();
            chart.Render();
        }


        // Internal 

        const int PIXELS_PER_POINT = 4;
        readonly System.Drawing.Color COLOR_GRAY = System.Drawing.Color.FromArgb(80, 80, 80);


        ScottPlot.Plottable.ScatterPlot _scatter;

        List<MeasureModel> _data = new List<MeasureModel>();

        private void Data2XY(out double[] x, out double[] y)
        {
            List<double> xi = new List<double>();
            List<double> yi = new List<double>();

            foreach (var i in _data)
            {
                xi.Add(i.Timestamp);
                yi.Add(i.Value);
            }

            x = xi.ToArray();
            y = yi.ToArray();
        }
    }
}
