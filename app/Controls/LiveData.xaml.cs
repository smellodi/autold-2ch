using System.Collections.Generic;
using System.Windows.Controls;

namespace AutOlD2Ch.Controls
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

            _scatter = new ScottPlot.Plottable.ScatterPlot(new double[] { 0 }, new double[] { 0 })
            {
                Color = LINE_COLOR,
                MarkerSize = 4f
            };

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

            chart.Refresh();
        }

        public void Empty()
        {
            _data.Clear();
            
            _scatter.Update(new double[] { 0 }, new double[] { 0 });

            chart.Plot.AxisAuto();
            chart.Render();
            //chart.Refresh();
        }

        public void Reset(double step, double baseline = 0)
        {
            _data.Clear();

            var count = ActualWidth / PIXELS_PER_POINT / step;
            var ts = Utils.Timestamp.Sec;

            while (_data.Count < count)
            {
                _data.Add(new MeasureModel
                {
                    Timestamp = ts + step * (_data.Count - count),
                    Value = baseline,
                });
            }

            Data2XY(out double[] x, out double[] y);

            _scatter.Update(x, y);
            chart.Plot.AxisAuto();
            chart.Render();
            //chart.Refresh();
        }

        public void Add(double timestamp, double value)
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
            //chart.Refresh();
        }


        // Internal 

        const int PIXELS_PER_POINT = 4;

        readonly System.Drawing.Color COLOR_GRAY = System.Drawing.Color.FromArgb(80, 80, 80);
        readonly List<MeasureModel> _data = new();
        readonly ScottPlot.Plottable.ScatterPlot _scatter;

        private void Data2XY(out double[] x, out double[] y)
        {
            var xi = new List<double>();
            var yi = new List<double>();

            if (_data.Count > 0)
            {
                foreach (var i in _data)
                {
                    xi.Add(i.Timestamp);
                    yi.Add(i.Value);
                }
            }
            else
            {
                xi.Add(0);
                yi.Add(0);
            }

            x = xi.ToArray();
            y = yi.ToArray();
        }
    }
}
