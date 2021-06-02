using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using Olfactory.Comm;

namespace Olfactory
{
    public class SyncLogger : LoggerBase<SyncLogger.Record>
    {
        public class Record
        {
            public static string DELIM => ",";
            public static string HEADER => $"Time [s]{DELIM}PID [mV]{DELIM}Loop [mA]{DELIM}QMa [std]{DELIM}QMb [std]{DELIM}Pa [mbar]{DELIM}Pb [mbar]{DELIM}Ta [C]{DELIM}Tb [C]{DELIM}Mark";

            public int Time => _time;

            public Record(ref MFCSample mfcSample, ref PIDSample pidSample, string[] events)
            {
                _time = (int)Math.Round((double)(mfcSample.Time > pidSample.Time ? mfcSample.Time : pidSample.Time) / 1000);

                _fields = new string[]
                {
                    _time.ToString(),
                    pidSample.PID.ToString("F4"),
                    pidSample.Loop.ToString("F4"),
                    mfcSample.A.MassFlow.ToString("F2"),
                    mfcSample.B.MassFlow.ToString("F2"),
                    mfcSample.A.Pressure.ToString("F2"),
                    mfcSample.B.Pressure.ToString("F2"),
                    mfcSample.A.Temperature.ToString("F2"),
                    mfcSample.B.Temperature.ToString("F2"),
                    string.Join(' ', events)
                };
            }

            public override string ToString()
            {
                return string.Join(DELIM, _fields);
            }

            // Internal

            readonly string[] _fields;
            int _time;
        }

        public static SyncLogger Instance => _instance = _instance ?? new();

        public bool HasRecords => _records.Count > 0;


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

        public void Finilize()
        {
            _timer.Stop();
        }

        // Internal methods

        static SyncLogger _instance = null;

        protected override string Header => Record.HEADER;

        DispatcherTimer _timer = new DispatcherTimer();

        MFCSample _mfcSample;
        PIDSample _pidSample;
        List<string> _events = new List<string>();

        Mutex _mutex = new Mutex();

        protected SyncLogger() : base()
        {
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                AddRecord();
            };

            _timer.Start();
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
