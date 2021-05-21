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
        ThresholdTest = 4,
    }

    public class Logger
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
                if (Data != null && Data.Length > 0)
                {
                    var data = string.Join(DELIM, Data);
                    return $"{Timestamp}{DELIM}{Source}{DELIM}{Type}{DELIM}{data}";
                }
                else
                {
                    return $"{Timestamp}{DELIM}{Type}";
                }
            }
        }

        public static Logger Instance => _instance switch { null => _instance = new Logger(), _ => _instance }; // is this line too complex? it simply return the instance is it exists, otherwise it creates a new instance and returns it

        public bool IsEnabled { get; set; } = true;
        public bool HasAnyRecord => _records.Count > 0;
        public bool HasTestRecords => _records.Any(r => (int)r.Source >= (int)LogSource.__MIN_TEST_ID);

        private Logger() { }

        public void Add(LogSource source, string type, params string[] data)
        {
            if (IsEnabled)
            {
                var record = new Record(source, type, data);
                _records.Add(record);
                //System.Diagnostics.Debug.WriteLine(record.ToString());
            }
        }

        public void SaveTo(string defaultFileName, string greeting = "")
        {
            var filename = defaultFileName;
            
            var dialogResult = MessageBox.Show(
                $"{greeting}\nSave data to 'Documents\\{defaultFileName}'?",
                Title,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (dialogResult == MessageBoxResult.Cancel)
            {
                return;
            }
            else if (dialogResult == MessageBoxResult.No)
            {
                filename = AskFileName(defaultFileName);
            }
            else
            {
                filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), filename);
            }

            Save(filename);
        }


        // Internal methods

        static Logger _instance = null;

        readonly List<Record> _records = new List<Record>();

        string Title => "Olfactory data logger";

        private string AskFileName(string defaultFileName)
        {
            var savePicker = new Microsoft.Win32.SaveFileDialog()
            {
                DefaultExt = "txt",
                FileName = defaultFileName,
                Filter = "Log files (*.txt)|*.txt"
            };

            if (savePicker.ShowDialog() ?? false)
            {
                return savePicker.FileName;
            }

            return null;
        }

        private void Save(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return;
            }

            var folder = Path.GetDirectoryName(filename);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            using (StreamWriter writer = File.CreateText(filename))
            {
                try
                {
                    writer.WriteLine(Record.HEADER);
                    writer.WriteLine(string.Join("\n", _records));

                    MessageBox.Show(
                        "Log data saved!",
                        Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    _records.Clear();
                }
                catch (Exception ex)
                {
                    var result = MessageBox.Show(
                        $"Failed to save data into '{filename}' file:\n\n{ex.Message}\n\nRetry?",
                        Title,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (result == MessageBoxResult.OK)
                    {
                        Save(AskFileName(filename));
                    }
                }
            }
        }
    }
}
