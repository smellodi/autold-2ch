using LiveCharts;
using System.Windows.Controls;

namespace Olfactory.Controls
{
    public partial class LiveMeasurement : UserControl
    {
        public ChartValues<double> Values { get; set; }

        public LiveMeasurement()
        {
            InitializeComponent();

            Values = new ChartValues<double>();

            DataContext = this;
        }

        public void Reset(double baseline = 0)
        {
            Values.Clear();
            while (Values.Count < ActualWidth / 4)
            {
                Values.Add(baseline);
            }
        }

        public void Add(double value)
        {
            if (Values.Count > ActualWidth / 4)
            {
                Values.RemoveAt(0);
            }

            Values.Add(value);
        }
    }
}
