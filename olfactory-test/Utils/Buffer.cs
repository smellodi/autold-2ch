using System;
using System.Collections.Generic;
using System.Text;

namespace Olfactory.Utils
{
    /// <summary>
    /// Generic buffer to store floating-point numbers
    /// </summary>
    public class Buffer
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
        public Buffer(int size = 10)
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

        public (PeakType, double) EstimatePeak()
        {
            if (!_isFull)
            {
                return (PeakType.None, 0);
            }

            int i = _pointer;
            int endIndex = (_pointer - 1) < 0 ? _size - 1 : _pointer - 1;

            //double[] dempData = new double[_size];
            //int j = 0;

            double? prevValue = null;
            double startValue = _buffer[i];
            double endValue = _buffer[endIndex];
            double min = startValue;
            double max = startValue;

            while (i != endIndex)
            {
                if (prevValue != null)
                {
                    double newValue = _buffer[i]; // * Damping + (double)prevValue * (1.0 - Damping);

                    min = Math.Min(newValue, min);
                    max = Math.Max(newValue, max);
                }

                prevValue = _buffer[i];

                if (++i == _size)
                {
                    i = 0;
                }
            }

            if ((startValue - min) > ChangeThreshold && (endValue - min) > ChangeThreshold)
            {
                return (PeakType.Lower, min);
            }

            if ((max - startValue) > ChangeThreshold && (max - endValue) > ChangeThreshold)
            {
                return (PeakType.Upper, max);
            }

            return (PeakType.None, 0);
        }


        // Internal

        const long MAX_ALLOWED_PAUSE = 500; // ms

        double[] _buffer;
        int _size;
        int _pointer = 0;
        bool _isFull = false;
        long _timestamp = 0;
    }
}
