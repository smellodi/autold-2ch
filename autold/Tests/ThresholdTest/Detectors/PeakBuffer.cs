using System;

namespace Olfactory.Tests.ThresholdTest
{
    /// <summary>
    /// Generic buffer to store floating-point numbers
    /// </summary>
    public class PeakBuffer
    {
        /// <summary>
        /// Peak type
        /// </summary>
        public enum PeakType
        {
            None,
            Upper,
            Lower
        }

        /// <summary>
        /// Defines the absolute value in mA to assume the loop current change reversed its direction
        /// </summary>
        public double ChangeThreshold { get; set; } = 0.1;

        /// <summary>
        /// Indicates whether the buffer is full already
        /// </summary>
        public bool IsFull => _isFull;

        /// <summary>
        /// Buffer size
        /// </summary>
        public int Size => _size;

        /// <summary>
        /// Average (mean) value
        /// </summary>
        public double Average
        {
            get
            {
                double sum = 0;
                int maxIndex = _isFull ? _size : _pointer;
                for (int i = 0; i < maxIndex; i += 1)
                {
                    sum += _buffer[i];
                }
                return maxIndex > 0 ? sum / maxIndex : 0;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="size">Buffer size</param>
        public PeakBuffer(int size = 10)
        {
            _size = size;
            _buffer = new double[_size];
        }

        public void Reset()
        {
            _pointer = 0;
            _isFull = false;
            _timestamp = 0;
        }

        /// <summary>
        /// Adds a new value to the buffer
        /// </summary>
        /// <param name="timestamp">Value timestamp</param>
        /// <param name="value">Value to add</param>
        public void Add(long timestamp, double value)
        {
            if (_timestamp > 0 && (timestamp - _timestamp) > MAX_ALLOWED_PAUSE)
            {
                Reset();
            }

            _timestamp = timestamp;
            _buffer[_pointer] = value;

            if (++_pointer == _size)
            {
                _pointer = 0;
                _isFull = true;
            }
        }

        public (PeakType, double) EstimatePeak(BreathingStage stage)
        {
            if (!_isFull)
            {
                return (PeakType.None, 0);
            }

            int i = _pointer;       // _pointer always points to the next empty cell, i.e. the one that holds the oldest value
            int endIndex = (_pointer - 1) < 0 ? _size - 1 : _pointer - 1;

            double? prevValue = null;
            double startValue = _buffer[i];
            double endValue = _buffer[endIndex];
            double min = startValue;
            double max = startValue;

            do
            {
                if (prevValue != null)
                {
                    double newValue = _buffer[i];

                    min = Math.Min(newValue, min);
                    max = Math.Max(newValue, max);
                }

                prevValue = _buffer[i];

                if (++i == _size)
                {
                    i = 0;
                }
            }
            while (i != _pointer);

            //* Original implementation fol long buffers (size = 15-20)

            var halfThreshold = ChangeThreshold / 2;

            // Order of the next two comparisons is important.
            // Now, the upper peak detection is prioterized, as it is connected
            // with temprerature starting to drop, i.e. with inhale

            if ((max - startValue) > halfThreshold && (max - endValue) > ChangeThreshold)
            {
                return (PeakType.Upper, max);
            }

            if ((startValue - min) > halfThreshold && (endValue - min) > ChangeThreshold)
            {
                return (PeakType.Lower, min);
            }
            
            /*/
            // New implementations for short buffers (size = 4-8)

            if (stage == BreathingStage.Inhale) // values going down... search when this stops
            {
                if ((_peakValue - min) > MIN_PEAK_AMPLITUDE && (max - endValue) < ChangeThreshold)
                {
                    _peakValue = min;
                    return (PeakType.Lower, min);
                }
                else if (max > _peakValue)
                {
                    _peakValue = max;
                }
            }
            else if (stage == BreathingStage.Exhale) // values going up... search when this stops
            {
                if ((max - _peakValue) > MIN_PEAK_AMPLITUDE && (endValue - min) < ChangeThreshold)
                {
                    _peakValue = max;
                    return (PeakType.Upper, max);
                }
                else if (min < _peakValue)
                {
                    _peakValue = min;
                }
            }
            else
            {
                _peakValue = (endValue + startValue) / 2;
                return (endValue - startValue) switch
                {
                    < 0 => (PeakType.Upper, max),
                    > 0 => (PeakType.Lower, min),
                    _ => (PeakType.None, 0)
                };
            }

            // --new */

            return (PeakType.None, 0);
        }


        // Internal

        const long MAX_ALLOWED_PAUSE = 500;     // ms
        const double MIN_PEAK_AMPLITUDE = 0.25; // mA

        readonly double[] _buffer;
        readonly int _size;
        
        int _pointer = 0;
        bool _isFull = false;
        long _timestamp = 0;

        double _peakValue = 0;
    }
}
