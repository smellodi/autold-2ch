using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace Olfactory
{
    public abstract class LoggerBase<T>
    {
        public void SaveTo(string defaultFileName, string greeting = "")
        {
            var filename = defaultFileName;

            if (!string.IsNullOrEmpty(greeting))
            {
                greeting += "\n";
            }

            var dialogResult = MessageBox.Show(
                $"{greeting}Would you like to save logged data into\r'{_folder}\\{defaultFileName}'?\n\nPress 'No' to change the name and/or folder.\nPress 'Cancel' to discard the data.",
                Title,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (dialogResult == MessageBoxResult.Cancel)
            {
                _records.Clear();
                return;
            }
            else if (dialogResult == MessageBoxResult.No)
            {
                filename = AskFileName(defaultFileName);
            }
            else
            {
                filename = Path.Combine(_folder, filename);
            }

            Save(filename);

            _records.Clear();
        }


        // Internal

        protected readonly List<T> _records = new List<T>();

        protected abstract string Header { get; }

        string _folder;

        string Title => "Olfactory data logger";


        protected LoggerBase()
        {
            _folder = Properties.Settings.Default.Logger_Folder;
            if (string.IsNullOrEmpty(_folder))
            {
                _folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
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

        protected void Save(string filename)
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
                    writer.WriteLine(Header);
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
