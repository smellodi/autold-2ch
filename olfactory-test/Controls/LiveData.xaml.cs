using System.Collections.Generic;
using System.Windows.Controls;

namespace Olfactory.Controls
{
    public partial class LiveData : UserControl
    {
        public static readonly System.Drawing.Color BRUSH_NEUTRAL = System.Drawing.Color.FromArgb(16, 160, 255);
        public static readonly System.Drawing.Color BRUSH_WARNING = System.Drawing.Color.Red;
        public static readonly System.Drawing.Color BRUSH_OK = System.Drawing.Color.Green;

        public static System.Drawing.Color OdorColor(Comm.MFC.OdorFlowsTo odorDirection)
        {
            return odorDirection switch
            {
                Comm.MFC.OdorFlowsTo.SystemAndUser => BRUSH_OK,
                Comm.MFC.OdorFlowsTo.SystemAndWaste => BRUSH_NEUTRAL,
                _ => BRUSH_WARNING
            };
        }

        public class MeasureModel
        {
            public double Timestamp { get; set; }
            public double Value { get; set; }
            public System.Drawing.Color Brush { get; set; }
        }

        public LiveData()
        {
            InitializeComponent();

            _scatter = new ScottPlot.Plottable.ScatterPlot(new double[] { 0 }, new double[] { 0 });
            _scatter.Color = System.Drawing.Color.FromArgb(16, 160, 255);
            _scatter.MarkerSize = 4f;

            chart.Plot.Add(_scatter);
            chart.Plot.XAxis.Color(System.Drawing.Color.Gray);
            chart.Plot.YAxis.Color(System.Drawing.Color.Gray);

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

        public void Reset(System.Drawing.Color brush, double baseline = 0)
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
                    Brush = brush
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

            _data.Add(new MeasureModel { Timestamp = timestamp, Value = value, Brush = brush ?? BRUSH_NEUTRAL });

            Data2XY(out double[] x, out double[] y);

            _scatter.Update(x, y);
            chart.Plot.AxisAuto();
            chart.Render();
        }


        // Internal 

        const int PIXELS_PER_POINT = 4;

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
