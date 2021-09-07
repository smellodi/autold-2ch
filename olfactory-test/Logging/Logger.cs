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
        public bool SaveTo(string defaultFileName, string greeting = "")
        {
            bool result = false;
            var filename = defaultFileName;
            if (PromptToSave(ref filename, greeting))
            {
                result = Save(filename, _records, Header);
            }

            if (result)
            {
                _records.Clear();
            }

            return result;
        }

        public void Clear()
        {
            _records.Clear();
        }


        // Internal

        protected readonly List<T> _records = new List<T>();

        protected abstract string Header { get; }

        string _folder;


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

            var saveInto = Utils.L10n.T("SaveDataInto");
            var pressNo = Utils.L10n.T("PressNoToChangeNameFolder");
            var pressCancel = Utils.L10n.T("PressCancelToDiscard");
            var dialogResult = MessageBox.Show(
                $"{greeting}{saveInto}\n'{_folder}\\{filename}'?\n\n{pressNo}\n{pressCancel}",
                Application.Current.MainWindow.Title,
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

                    var dataSavedInto = Utils.L10n.T("DataSavedInto");
                    MessageBox.Show(
                        $"{dataSavedInto}\n'{filename}'",
                        Application.Current.MainWindow.Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return true;
                }
                catch (Exception ex)
                {
                    var failedToSave = Utils.L10n.T("FailedToSave");
                    var retry = Utils.L10n.T("Retry");
                    var result = MessageBox.Show(
                        $"{failedToSave}\n'{filename}':\n\n{ex.Message}\n\n{retry}",
                        Application.Current.MainWindow.Title,
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
