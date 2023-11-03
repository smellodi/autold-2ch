using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using AutOlD2Ch.Comm;

namespace AutOlD2Ch
{
    public class SyncLogger : Logger<SyncLogger.Record>, IDisposable
    {
        public class Record
        {
            public static string DELIM => ",";
            public static string HEADER => $"Time [ms]{DELIM}PID [mV]{DELIM}Loop [mA]{DELIM}QMa [std]{DELIM}QMb [std]{DELIM}QMc [std]{DELIM}Pa [mbar]{DELIM}Pb [mbar]{DELIM}Pc [mbar]{DELIM}Ta [C]{DELIM}Tb [C]{DELIM}Tc [C]{DELIM}Mark";

            public long Time => _time;

            public Record(ref MFCSample mfcSample, ref PIDSample pidSample, string[] events)
            {
                _time = mfcSample.Time > pidSample.Time ? mfcSample.Time : pidSample.Time;

                _fields = new string[]
                {
                    _time.ToString(),
                    pidSample.PID.ToString("F4"),
                    pidSample.Loop.ToString("F4"),
                    mfcSample.A.MassFlow.ToString("F2"),
                    mfcSample.B.MassFlow.ToString("F2"),
                    mfcSample.C.MassFlow.ToString("F2"),
                    mfcSample.A.Pressure.ToString("F2"),
                    mfcSample.B.Pressure.ToString("F2"),
                    mfcSample.C.Pressure.ToString("F2"),
                    mfcSample.A.Temperature.ToString("F2"),
                    mfcSample.B.Temperature.ToString("F2"),
                    mfcSample.C.Temperature.ToString("F2"),
                    string.Join(' ', events)
                };
            }

            public override string ToString()
            {
                return string.Join(DELIM, _fields);
            }

            // Internal

            readonly string[] _fields;
            readonly long _time;
        }

        public static SyncLogger Instance => _instance ??= new();

        public bool HasRecords => _records.Count > 0;

        /// <summary>
        /// Sets the interval and starts logging
        /// </summary>
        /// <param name="interval">Time interval in milliseconds</param>
        public void Start(int interval)
        {
            _timer.Interval = interval;
            _timer.Start();
        }

        public void Add(MFCSample mfcSample)
        {
            lock (_mutex)
            {
                _mfcSample = mfcSample;
            }
        }

        public void Add(PIDSample pidSample)
        {
            lock (_mutex)
            {
                _pidSample = pidSample;
            }
        }

        public void Add(string evt)
        {
            lock (_mutex)
            {
                _events.Add(evt);
            }
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void Dispose()
        {
            _timer.Dispose();
            GC.SuppressFinalize(this);
        }

        // Internal methods

        static SyncLogger _instance = null;

        protected override string Header => Record.HEADER;

        readonly List<string> _events = new();
        readonly System.Timers.Timer _timer = new();
        readonly Mutex _mutex = new();

        MFCSample _mfcSample;
        PIDSample _pidSample;

        protected SyncLogger() : base()
        {
            _timer.Elapsed += (s, e) => Dispatcher.CurrentDispatcher.Invoke(AddRecord);
        }

        private void AddRecord()
        {
            lock (_mutex)
            {
                var record = new Record(ref _mfcSample, ref _pidSample, _events.ToArray());
                if (record.Time > 0)
                {
                    _records.Add(record);
                    _events.Clear();
                }
            }
        }
    }
}
