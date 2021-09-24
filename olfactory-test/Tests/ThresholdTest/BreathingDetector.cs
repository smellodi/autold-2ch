using System;
using System.IO;
using System.Text;
using System.Windows.Media;

namespace Olfactory.Tests.ThresholdTest
{
    /// <summary>
    /// Detects breathing stages from PID Loop values
    /// </summary>
    public class BreathingDetector
    {
        /// <summary>
        /// Breathing stages
        /// </summary>
        public enum Stage
        {
            None,
            Inhale,
            Exhale
        }

        public static Brush InhaleBrush => Brushes.LightSkyBlue;
        public static Brush ExhaleBrush => Brushes.Pink;

        public event EventHandler<Stage> StageChanged;


        /// <summary>
        /// Breathing stage
        /// </summary>
        public Stage BreathingStage { get; private set; } = Stage.None;

        /// <summary>
        /// Feed loop values when reading them from PID device
        /// </summary>
        /// <param name="timestamp">Sample timestamp</param>
        /// <param name="value">Loop value in mA</param>
        /// <returns>'True' if the breathing stage was changed, 'False' otherwise</returns>
        public bool Feed(long timestamp, double value)
        {
            bool isStageChanged = false;

            _buffer.Add(timestamp, value);
            var ( type, peak ) = _buffer.EstimatePeak();

            if (type != _currentPeakType)
            {
                _currentPeakType = type;

                var breathingStage = _currentPeakType switch
                {
                    Utils.PeakBuffer.PeakType.Lower => Stage.Exhale,
                    Utils.PeakBuffer.PeakType.Upper => Stage.Inhale,
                    _ => BreathingStage
                };

                if (breathingStage != BreathingStage)
                {
                    _peakMax = breathingStage == Stage.Inhale ? peak : _peakMin;
                    _peakMin = breathingStage == Stage.Exhale ? peak : _peakMax;
                    if (_peakMin > 0 && _peakMax > 0)
                    {
                        AdjustThreshold();
                    }

                    isStageChanged = true;
                    BreathingStage = breathingStage;

                    _logger.Add(LogSource.ThTest, "breathing", breathingStage.ToString());

                    StageChanged?.Invoke(this, BreathingStage);
                }
            }

            return isStageChanged;
        }

        /// <summary>
        /// [Used in testing]
        /// Detects breathing stage from CSV files.
        /// The detected stages are added to the end of lines when detected.
        /// Another debugging data could be added as well
        /// </summary>
        /// <param name="filename">CSV filename</param>
        /// <returns>String builder with the data appended to the end of each line</returns>
        public static StringBuilder Test(string filename)
        {
            var det = new BreathingDetector();
            StringBuilder sb = new();

            using (var stream = new StreamReader(filename))
            {
                while (!stream.EndOfStream)
                {
                    var line = stream.ReadLine();
    
                    var p = line.Split(',');
                    if (p.Length >= 9 && long.TryParse(p[0], out long timestamp) && double.TryParse(p[2], out double loop) && det.Feed(timestamp, loop))
                    {
                        sb.AppendLine(line + $",{det.BreathingStage}");
                    }
                    else
                    {
                        sb.AppendLine(line + $",{det._buffer.ChangeThreshold}");
                    }
                }
            }

            return sb;
        }

        // Internal

        const double MIN_THRESHOLD = 0.1;
        const double MAX_THRESHOLD = 0.5;
        const double PEAK_SHARE_FROM_EXTREAMS = 0.1; // threshold is 1/10 from the diff of extream values
        const double NEW_PEAK_WEIGHT = 0.2;          // the new threshold has small weight, so the threshold change is not very dramatic after each inhale/exhale

        readonly Utils.PeakBuffer _buffer = new(18); // 5 samples = 1 second

        readonly FlowLogger _logger = FlowLogger.Instance;

        Utils.PeakBuffer.PeakType _currentPeakType = Utils.PeakBuffer.PeakType.None;

        double _peakMin = 0;
        double _peakMax = 0;

        private void AdjustThreshold()
        {
            var currentThreshold = _buffer.ChangeThreshold;
            var peaksDiff = _peakMax - _peakMin;
            var newThreshold = peaksDiff * PEAK_SHARE_FROM_EXTREAMS;
            newThreshold = newThreshold * NEW_PEAK_WEIGHT + currentThreshold * (1f - NEW_PEAK_WEIGHT);

            _buffer.ChangeThreshold = Math.Max(MIN_THRESHOLD, Math.Min(MAX_THRESHOLD, newThreshold));
        }
    }
}
