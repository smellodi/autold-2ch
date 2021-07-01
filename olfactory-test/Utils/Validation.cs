using System.Globalization;
using System.Linq;
using System.Windows.Controls;

namespace Olfactory.Utils
{
    public class Validation
    {
        public static readonly NumberStyles INTEGER = NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        public static readonly NumberStyles FLOAT = NumberStyles.Float;

        public enum ValueFormat
        {
            Integer = NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            Float = NumberStyles.Float
        }

        public readonly TextBox Source;
        public readonly double Min;
        public readonly double Max;
        public readonly ValueFormat Format;

        public bool IsList => _listDelim != null;
        public bool AcceptsExpression => _expressionDelims != null;
        public string Value => _value;

        public bool IsValid
        {
            get
            {
                if (_listDelim != null)
                {
                    var chunks = Source.Text
                        .Split(_listDelim ?? ' ')
                        .Where(v => !string.IsNullOrWhiteSpace(v));
                    if (chunks.Count() == 0)
                    {
                        _code = ValidityViolationCode.EmptyList;
                    }
                    else
                    {
                        chunks.All(chunk => IsValidValueOrExpression(chunk));
                    }
                }
                else
                {
                    IsValidValueOrExpression(Source.Text);
                }

                return _code == ValidityViolationCode.OK;
            }
        }

        public Validation(TextBox textbox, double min, double max, ValueFormat format, char? listDelim = null, char[] expressionDelims = null)
        {
            Source = textbox;
            Min = min;
            Max = max;
            Format = format;
            _listDelim = listDelim;
            _expressionDelims = expressionDelims;

            _value = Source.Text;
        }

        public override string ToString()
        {
            return _code switch
            {
                ValidityViolationCode.EmptyList => "The list of values is empty",
                ValidityViolationCode.InvalidExpression => $"The expression '{_value}' is invalid, only '{_expressionDelims}' operations are allow",
                ValidityViolationCode.InvalidFormat => $"The type of value '{_value}' is invalid, it must be a '{Format}' number",
                ValidityViolationCode.TooLarge => $"The value '{_value}' is too large, is must be no greater than '{Max}'",
                ValidityViolationCode.TooSmall => $"The value '{_value}' is too small, is must be no smaller than '{Min}'",
                _ => "unknown error",
            };
        }

        // Internal

        enum ValidityViolationCode
        {
            OK,
            InvalidFormat,
            TooSmall,
            TooLarge,
            EmptyList,
            InvalidExpression
        }

        ValidityViolationCode _code = ValidityViolationCode.OK;
        string _value;
        char? _listDelim;
        char[] _expressionDelims;

        private bool IsValidValueOrExpression(string value)
        {
            if (_expressionDelims != null)
            {
                var exprValues = value.Split(_expressionDelims);
                if (exprValues.Length > 1)
                {
                    return exprValues.All(exprValue => IsValidValueOrExpression(exprValue));
                }
            }

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
}
