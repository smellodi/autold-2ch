using System;
using System.Collections.Generic;
using System.IO;

namespace AutOlD2Ch;

[Flags]
public enum LogSource
{
    MFC = 1,
    PID = 2,
    COM = MFC | PID,

    __MIN_TEST_ID = 4,
    OdProd = 4,
    Comparison = 5,
    LptCtrl = 6,
}

public enum SavingResult
{
    Save,
    Discard,
    Cancel,
}

public abstract class Logger<T> where T : class
{
    public SavingResult SaveTo(string defaultFileName, string greeting = "")
    {
        var filename = defaultFileName;
        var result = PromptToSave(ref filename, greeting);
        if (result == SavingResult.Save)
        {
            result = Save(filename, _records, Header) ? SavingResult.Save : SavingResult.Cancel;
        }

        if (result == SavingResult.Save || result == SavingResult.Discard)
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

    protected readonly List<T> _records = new();

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

    protected SavingResult PromptToSave(ref string? filename, string greeting = "")
    {
        if (!string.IsNullOrEmpty(greeting))
        {
            greeting += "\n";
        }

        var saveInto = Utils.L10n.T("SaveDataInto");
        var pressNo = Utils.L10n.T("PressNoToChangeNameFolder");
        var pressDicard = Utils.L10n.T("PressDiscard");
        var pressCancel = Utils.L10n.T("PressCancel");
        var answer = Utils.MsgBox.Ask(
            App.Name + " - " + Utils.L10n.T("Logger"),
            $"{greeting}{saveInto}\n'{_folder}\\{filename}'?\n\n{pressNo}\n{pressDicard}\n{pressCancel}",
            Utils.MsgBox.Button.Yes, Utils.MsgBox.Button.No, Utils.MsgBox.Button.Discard, Utils.MsgBox.Button.Cancel);

        if (answer == Utils.MsgBox.Button.Discard)
        {
            return SavingResult.Discard;
        }
        else if (answer == Utils.MsgBox.Button.No)
        {
            filename = AskFileName(filename);
            return string.IsNullOrEmpty(filename) ? SavingResult.Cancel : SavingResult.Save;
        }
        else if (answer == Utils.MsgBox.Button.Yes)
        {
            filename = Path.Combine(_folder, filename ?? "");
            return SavingResult.Save;
        }

        return SavingResult.Cancel;
    }

    protected string? AskFileName(string? defaultFileName)
    {
        var savePicker = new Microsoft.Win32.SaveFileDialog()
        {
            DefaultExt = "txt",
            FileName = defaultFileName,
            Filter = "Log files (*.txt)|*.txt"
        };

        if (savePicker.ShowDialog() ?? false)
        {
            _folder = Path.GetDirectoryName(savePicker.FileName) ?? "";
            Properties.Settings.Default.Logger_Folder = _folder;
            Properties.Settings.Default.Save();

            return savePicker.FileName;
        }

        return null;
    }

    protected bool Save(string? filename, IEnumerable<object> records, string header = "")
    {
        if (string.IsNullOrEmpty(filename))
        {
            return false;
        }

        var folder = Path.GetDirectoryName(filename) ?? "";

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
                Utils.MsgBox.Notify(
                    App.Name + " - " + Utils.L10n.T("Logger"),
                    $"{dataSavedInto}\n'{filename}'",
                    Utils.MsgBox.Button.OK);

                return true;
            }
            catch (Exception ex)
            {
                var failedToSave = Utils.L10n.T("FailedToSave");
                var retry = Utils.L10n.T("Retry");
                var answer = Utils.MsgBox.Ask(
                    App.Name + " - " + Utils.L10n.T("Logger"),
                    $"{failedToSave}\n'{filename}':\n\n{ex.Message}\n\n{retry}",
                    Utils.MsgBox.Button.Yes, Utils.MsgBox.Button.No);
                if (answer == Utils.MsgBox.Button.Yes)
                {
                    return Save(AskFileName(filename), records, header);
                }
            }
        }

        return false;
    }
}
