using System;
using System.Collections.Generic;
using System.IO;
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

    public abstract class Logger<T> where T : class
    {
        public void SaveTo(string defaultFileName, string greeting = "")
        {
            var filename = defaultFileName;
            if (PromptToSave(ref filename, greeting))
            {
                Save(filename, _records, Header);
            }

            _records.Clear();
        }


        // Internal

        protected readonly List<T> _records = new List<T>();

        protected abstract string Header { get; }

        string _folder;

        string Title => "Olfactory data logger";


        protected Logger()
        {
            _folder = Properties.Settings.Default.Logger_Folder;
            if (string.IsNullOrEmpty(_folder))
            {
                _folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        protected bool PromptToSave(ref string filename, string greeting = "")
        {
            if (!string.IsNullOrEmpty(greeting))
            {
                greeting += "\n";
            }

            var dialogResult = MessageBox.Show(
                $"{greeting}Would you like to save data into\n'{_folder}\\{filename}'?\n\nPress 'No' to change the name and/or folder.\nPress 'Cancel' to discard the data.",
                Title,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (dialogResult == MessageBoxResult.Cancel)
            {
                return false;
            }
            else if (dialogResult == MessageBoxResult.No)
            {
                filename = AskFileName(filename);
                return !string.IsNullOrEmpty(filename);
            }
            else
            {
                filename = Path.Combine(_folder, filename);
            }

            return true;
        }

        protected string AskFileName(string defaultFileName)
        {
            var savePicker = new Microsoft.Win32.SaveFileDialog()
            {
                DefaultExt = "txt",
                FileName = defaultFileName,
                Filter = "Log files (*.txt)|*.txt"
            };

            if (savePicker.ShowDialog() ?? false)
            {
                _folder = Path.GetDirectoryName(savePicker.FileName);
                Properties.Settings.Default.Logger_Folder = _folder;
                Properties.Settings.Default.Save();

                return savePicker.FileName;
            }

            return null;
        }

        protected bool Save(string filename, IEnumerable<object> records, string header = "")
        {
            if (string.IsNullOrEmpty(filename))
            {
                return false;
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
                    if (!string.IsNullOrEmpty(header))
                    {
                        writer.WriteLine(header);
                    }

                    writer.WriteLine(string.Join("\n", records));

                    MessageBox.Show(
                        $"Data saved into '{filename}'",
                        Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return true;
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
                        return Save(AskFileName(filename), records, header);
                    }
                }
            }

            return false;
        }
    }
}
