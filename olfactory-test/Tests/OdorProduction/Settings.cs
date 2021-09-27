using System.Linq;
using System.Collections.Generic;

namespace Olfactory.Tests.OdorProduction
{
    public class Settings
    {
        public const int MIN_FLOW_DURATION = 10; // ms
        public static readonly char LIST_DELIM = ',';
        public static readonly char[] EXPR_OPS = new char[] { 'x', '*' };

        public double FreshAir;
        public (double, int)[] OdorQuantities; // pairs of "speed_ml/min" x "duration_ms"
        public int InitialPause;
        public double OdorFlowDuration;
        public int FinalPause;
        public int PIDReadingInterval;
        public bool Valve2ToUser;
        public bool UseFeedbackLoopToReachLevel;
        public bool UseFeedbackLoopToKeepLevel;

        public Settings()
        {
            var settings = Properties.Settings.Default;

            FreshAir = settings.Test_OP_FreshAir;
            OdorQuantities = ParseOdorQuantinies(settings.Test_OP_OdorQuantityList);
            InitialPause = settings.Test_OP_InitialPause;
            OdorFlowDuration = settings.Test_OP_OdorFlowDuration;
            FinalPause = settings.Test_OP_FinalPause;
            PIDReadingInterval = settings.Test_OP_PIDReadingInterval;
            Valve2ToUser = settings.Test_OP_Valve2User;
            UseFeedbackLoopToReachLevel = settings.Test_OP_UseFeedbackLoopToReachLevel;
            UseFeedbackLoopToKeepLevel = settings.Test_OP_UseFeedbackLoopToKeepLevel;
        }

        public void Save()
        {
            var settings = Properties.Settings.Default;

            settings.Test_OP_FreshAir = FreshAir;
            settings.Test_OP_OdorQuantityList = OdorQuantitiesAsString();
            settings.Test_OP_InitialPause = InitialPause;
            settings.Test_OP_OdorFlowDuration = OdorFlowDuration;
            settings.Test_OP_FinalPause = FinalPause;
            settings.Test_OP_PIDReadingInterval = PIDReadingInterval;
            settings.Test_OP_Valve2User = Valve2ToUser;
            settings.Test_OP_UseFeedbackLoopToReachLevel = UseFeedbackLoopToReachLevel;
            settings.Test_OP_UseFeedbackLoopToKeepLevel = UseFeedbackLoopToKeepLevel;

            settings.Save();
        }

        public string OdorQuantitiesAsString()
        {
            var quantities = new List<string>();

            double mlmin = -1;
            int ms = 0;
            int count = 0;

            foreach (var (speed, duration) in OdorQuantities)
            {
                if (count > 0 && (speed != mlmin || duration != ms))
                {
                    quantities.Add(ToExpression(mlmin, ms, count));
                    count = 0;
                }

                mlmin = speed;
                ms = duration;
                count++;
            }

            if (count > 0)
            {
                quantities.Add(ToExpression(mlmin, ms, count));
            }

            return string.Join(LIST_DELIM + " ", quantities);
        }

        public static (double, int)[] ParseOdorQuantinies(string input)
        {
            return AsOdorQuantities(input.Split(LIST_DELIM));
        }

        public static double GetOdorQuantitiesLongestDuration(string input)
        {
            return ParseOdorQuantinies(input).Max(value => value.Item2);
        }


        // Internal

        private static (double, int)[] AsOdorQuantities(string expression)
        {
            var exprValues = expression.Split(EXPR_OPS);
            if (exprValues.Length == 1)
            {
                return new (double, int)[] { (double.Parse(expression), 0) };
            }
            else if (exprValues.Length == 2)
            {
                var mlmin = double.Parse(exprValues[0]);
                var secondParam = int.Parse(exprValues[1]);
                if (secondParam < MIN_FLOW_DURATION)
                {
                    var values = new List<(double, int)>();
                    for (int i = 0; i < secondParam; i++)
                    {
                        values.Add((mlmin, 0));
                    }
                    return values.ToArray();
                }
                else
                {
                    return new (double, int)[] { (mlmin, secondParam) };
                }
            }
            else
            {
                var mlmin = double.Parse(exprValues[0]);
                var ms = int.Parse(exprValues[1]);
                var count = int.Parse(exprValues[2]);
                var values = new List<(double, int)>();
                for (int i = 0; i < count; i++)
                {
                    values.Add((mlmin, ms));
                }
                return values.ToArray();
            };
        }

        private static (double, int)[] AsOdorQuantities(string[] expressions)
        {
            return expressions.SelectMany(expression => AsOdorQuantities(expression)).ToArray();
        }

        private static string ToExpression(double mlmin, int ms, int count)
        {
            var op = EXPR_OPS[0];
            return mlmin.ToString() + (ms, count) switch
            {
                ( > 0, > 1) => $"{op}{ms}{op}{count}",
                ( > 0, 1) => $"{op}{ms}",
                (0, > 1) => $"{op}{count}",
                _ => ""
            };
        }
    }
}
