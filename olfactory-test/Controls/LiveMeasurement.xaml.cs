using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System.Windows.Controls;

// check the manual on https://lvcharts.net/

namespace Olfactory.Controls
{
    public partial class LiveMeasurement : UserControl
    {
        public class MeasureModel
        {
            public double Timestamp { get; set; }
            public double Value { get; set; }
        }

        public SeriesCollection SeriesCollection { get; set; }
        //public ChartValues<MeasureModel> Values { get; set; }

        public LiveMeasurement()
        {
            InitializeComponent();

            var mapper = Mappers.Xy<MeasureModel>().X(v => v.Timestamp).Y(v => v.Value);

            SeriesCollection = new SeriesCollection(mapper);
            SeriesCollection.Add(new LineSeries
                {
                    Title = "",
                    Values = new ChartValues<MeasureModel>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Fill = System.Windows.Media.Brushes.Transparent,
                }
            );

            DataContext = this;
        }

        public void Reset(double baseline = 0)
        {
            var values = SeriesCollection[0].Values;
            values.Clear();

            _startTimestamp = Utils.Timestamp.Value;
            var count = ActualWidth / 4;
            var index = 0;

            while (values.Count < count)
            {
                values.Add(new MeasureModel { Timestamp = (index - count), Value = baseline });
            }
        }

        public void Add(double timestamp,  double value)
        {
            var values = SeriesCollection[0].Values;
            if (values.Count > ActualWidth / 4)
            {
                values.RemoveAt(0);
            }

            values.Add(new MeasureModel { Timestamp = timestamp, Value = value });
        }

        // Internal

        long _startTimestamp = 0;
    }
}
