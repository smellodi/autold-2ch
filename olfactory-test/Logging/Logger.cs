using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Olfactory
{
    [Flags]
    public enum LogSource
    {
        MFC = 1,
        PID = 2,
        COM = MFC | PID,

        __MIN_TEST_ID = 4,
        ThTest = 4,
        OdProd = 8,
    }

    public class Logger : LoggerBase<Logger.Record>
    {
        public class Record
        {
            public static string DELIM => "\t";
            public static string HEADER => $"ts{DELIM}source{DELIM}type{DELIM}data";

            public long Timestamp { get; private set; }
            public LogSource Source { get; private set; }
            public string Type { get; private set; }
            public string[] Data { get; private set; }

            public Record(LogSource source, string type, string[] data)
            {
                Timestamp = Utils.Timestamp.Value;
                Source = source;
                Type = type;
                Data = data;
            }

            public override string ToString()
            {
                var result = $"{Timestamp}{DELIM}{Source}{DELIM}{Type}";
                if (Data != null && Data.Length > 0)
                {
                    result += DELIM + string.Join(DELIM, Data);
                }

                return result;
            }
        }

        public static Logger Instance => _instance = _instance ?? new();

        public bool IsEnabled { get; set; } = true;
        public bool HasAnyRecord => _records.Count > 0;
        public bool HasTestRecords => _records.Any(r => (int)r.Source >= (int)LogSource.__MIN_TEST_ID);


        public void Add(LogSource source, string type, params string[] data)
        {
            if (IsEnabled)
            {
                var record = new Record(source, type, data);
                _records.Add(record);
                //System.Diagnostics.Debug.WriteLine(record.ToString());
            }
        }


        // Internal methods

        static Logger _instance = null;

        protected override string Header => Record.HEADER;

        protected Logger() : base() { }
    }
}
