using System;
using System.Linq;
using System.Collections.Generic;

namespace Olfactory.Tests.Comparison
{
    public enum Gas
    {
        nButanol,
        IPA
    }

    public enum Mixture
    {
        Odor1,
        Odor2,
        Mix50
    }

    public class MixturePair
    {
        public const string DELIM = ",";
        public Mixture Mix1 { get; set; }
        public Mixture Mix2 { get; set; }
        public static MixturePair FromString(string str)
        {
            var p = str.Split(DELIM);
            return new MixturePair()
            {
                Mix1 = (Mixture)Enum.Parse(typeof(Mixture), p[0]),
                Mix2 = (Mixture)Enum.Parse(typeof(Mixture), p[1]),
            };
        }
        public override string ToString()
        {
            return $"{Mix1}{DELIM} {Mix2}";
        }
    }

    public class Settings
    {
        //public const int MIN_FLOW_DURATION = 10; // ms

        public double FreshAirFlow;
        public double OdorFlow;
        public Gas Gas1;
        public Gas Gas2;
        public MixturePair[] PairsOfMixtures;
        public double InitialPause;
        public double OdorFlowDuration;
        //public int PIDReadingInterval;

        public int OdorFlowDurationMs => (int)(OdorFlowDuration * 1000);
        public Dictionary<string, string> Params => new()
        {
            { "fresh", FreshAirFlow.ToString() },
            { "odor", OdorFlow.ToString() },
            { "gas1", Gas1.ToString() },
            { "gas2", Gas2.ToString() },
            { "pause", InitialPause.ToString() },
            { "flow", OdorFlowDuration.ToString() },
        };

        public Settings()
        {
            var settings = Properties.Settings.Default;

            FreshAirFlow = settings.Test_CMP_FreshAirFlow;
            OdorFlow = settings.Test_CMP_OdorFlow;
            Gas1 = (Gas)settings.Test_CMP_Gas1;
            Gas2 = (Gas)settings.Test_CMP_Gas2;
            PairsOfMixtures = ParsePairsOfMixtures(settings.Test_CMP_Mixtures, out string _);
            InitialPause = settings.Test_CMP_InitialPause;
            OdorFlowDuration = settings.Test_CMP_OdorFlowDuration;
            //PIDReadingInterval = settings.Test_OP_PIDReadingInterval;
        }

        public void Save()
        {
            var settings = Properties.Settings.Default;

            settings.Test_CMP_FreshAirFlow = FreshAirFlow;
            settings.Test_CMP_OdorFlow = OdorFlow;
            settings.Test_CMP_Gas1 = (int)Gas1;
            settings.Test_CMP_Gas2 = (int)Gas2;
            settings.Test_CMP_Mixtures = SerializeMixtures();
            settings.Test_CMP_InitialPause = InitialPause;
            settings.Test_CMP_OdorFlowDuration = OdorFlowDuration;
            //settings.Test_OP_PIDReadingInterval = PIDReadingInterval;

            settings.Save();
        }

        public string SerializeMixtures()
        {
            return PairsOfMixtures != null ? string.Join('\n', PairsOfMixtures.Select(pair => pair.ToString())) : "";
        }

        public static MixturePair[] ParsePairsOfMixtures(string input, out string error)
        {
            var mixtures = input.Split('\n');
            if (mixtures.Length < 1)
            {
                error = Utils.L10n.T("EmptyList");
            }

            var result = new List<MixturePair>();

            foreach (var mixtureStr in mixtures)
            {
                try
                {
                    MixturePair mixture = MixturePair.FromString(mixtureStr);
                    result.Add(mixture);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return null;
                }
            }

            error = null;
            return result.ToArray();
        }
    }
}
