using System;
using System.Globalization;
using System.Windows.Controls;

namespace AutOlD2Ch.Utils;

public class Validation
{
    public static readonly NumberStyles INTEGER = NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
    public static readonly NumberStyles FLOAT = NumberStyles.Float;

    public enum ValueFormat
    {
        Integer = NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
        Float = NumberStyles.Float,
        Unknown = NumberStyles.Any,
    }

    public readonly Control Source;
    public readonly double Min;
    public readonly double Max;
    public readonly ValueFormat Format;
    public readonly string? ExternalError;

    public string? Value => _value;
    public double? AsNumber => IsValid && _value != null ? double.Parse(_value) : null;

    public bool IsValid
    {
        get
        {
            IsValidValue(Value);
            return _code == ValidityViolationCode.OK;
        }
    }

    public Validation(TextBox textbox, double min, double max, ValueFormat format)
    {
        Source = textbox;
        Min = min;
        Max = max;
        Format = format;

        _value = (Source as TextBox)?.Text;
    }

    public Validation(TextBox textbox, string value, double min, double max, ValueFormat format)
    {
        Source = textbox;
        Min = min;
        Max = max;
        Format = format;

        _value = value;
    }

    public Validation(Control textbox, string? externalError)
    {
        Source = textbox;
        Format = ValueFormat.Unknown;
        ExternalError = externalError;

        _code = ValidityViolationCode.ExternallyDetectedError;
        _value = (Source as TextBox)?.Text;
    }

    public override string ToString()
    {
        return _code switch
        {
            ValidityViolationCode.EmptyList => L10n.T("EmptyList"),
            ValidityViolationCode.InvalidFormat => string.Format(L10n.T("ValueFormatNotValid"), _value, Format),
            ValidityViolationCode.TooLarge => string.Format(L10n.T("ValueTooLarge"), _value, Max),
            ValidityViolationCode.TooSmall => string.Format(L10n.T("ValueTooSmall"), _value, Min),
            ValidityViolationCode.ExternallyDetectedError => ExternalError ?? "",
            _ => "unknown error",
        };
    }

    public static bool Do(TextBox textbox, double min, double max, EventHandler<int> action)
    {
        return Do(textbox, min, max, ValueFormat.Integer, (object? s, double e) => action(s, (int)e));
    }

    public static bool Do(TextBox textbox, double min, double max, EventHandler<double> action)
    {
        return Do(textbox, min, max, ValueFormat.Float, (object? s, double e) => action(s, e));
    }

    // Internal

    enum ValidityViolationCode
    {
        OK,
        InvalidFormat,
        TooSmall,
        TooLarge,
        EmptyList,
        ExternallyDetectedError,
    }

    ValidityViolationCode _code = ValidityViolationCode.OK;
    string? _value;

    private static bool Do(TextBox textbox, double min, double max, ValueFormat format, EventHandler<double> action)
    {
        var validation = new Validation(textbox, min, max, format);
        if (!validation.IsValid)
        {
            var msg = L10n.T("CorrectAndTryAgain");
            MsgBox.Error(
                App.Name + " - " + L10n.T("Validator"),
                $"{validation}.\n{msg}");
            validation.Source.Focus();
            (validation.Source as TextBox)?.SelectAll();

            return false;
        }

        action(validation, validation.AsNumber ?? 0);
        return true;
    }

    private bool IsValidValue(string? value)
    {
        _value = value;

        // not expresion, or the expression is a simple value
        if (Format == ValueFormat.Float)
        {
            if (!double.TryParse(value, FLOAT, null, out double val))
            {
                _code = ValidityViolationCode.InvalidFormat;
            }
            else if (val < Min)
            {
                _code = ValidityViolationCode.TooSmall;
            }
            else if (val > Max)
            {
                _code = ValidityViolationCode.TooLarge;
            }
        }
        else if (Format == ValueFormat.Integer)
        {
            if (!int.TryParse(value, INTEGER, null, out int val))
            {
                _code = ValidityViolationCode.InvalidFormat;
            }
            else if (val < Min)
            {
                _code = ValidityViolationCode.TooSmall;
            }
            else if (val > Max)
            {
                _code = ValidityViolationCode.TooLarge;
            }
        }
        else
        {
            _code = ValidityViolationCode.InvalidFormat;
        }

        return _code == ValidityViolationCode.OK;
    }
}
