using System;
using System.Linq;
using System.Collections.Generic;

namespace Olfactory2Ch.Tests.Comparison
{
    public enum Mixture
    {
        Odor1,
        Odor2,
        // All other possible values must in the form "Mix[1-99]"
        Mix01, Mix02, Mix03, Mix04, Mix05, Mix06, Mix07, Mix08, Mix09,
        Mix10, Mix11, Mix12, Mix13, Mix14, Mix15, Mix16, Mix17, Mix18, Mix19,
        Mix20, Mix21, Mix22, Mix23, Mix24, Mix25, Mix26, Mix27, Mix28, Mix29,
        Mix30, Mix31, Mix32, Mix33, Mix34, Mix35, Mix36, Mix37, Mix38, Mix39,
        Mix40, Mix41, Mix42, Mix43, Mix44, Mix45, Mix46, Mix47, Mix48, Mix49,
        Mix50, Mix51, Mix52, Mix53, Mix54, Mix55, Mix56, Mix57, Mix58, Mix59,
        Mix60, Mix61, Mix62, Mix63, Mix64, Mix65, Mix66, Mix67, Mix68, Mix69,
        Mix70, Mix71, Mix72, Mix73, Mix74, Mix75, Mix76, Mix77, Mix78, Mix79,
        Mix80, Mix81, Mix82, Mix83, Mix84, Mix85, Mix86, Mix87, Mix88, Mix89,
        Mix90, Mix91, Mix92, Mix93, Mix94, Mix95, Mix96, Mix97, Mix98, Mix99,
    }

    public enum GasSniffer
    {
        Human,
        DMS
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

    public enum Stage
    {
        Practice,
        Test
    }

    public class Settings
    {
        //public const int MIN_FLOW_DURATION = 10; // ms

        public GasSniffer Sniffer;
        public double FreshAirFlow;
        public double PracticeOdorFlow;
        public double TestOdorFlow;
        public Comm.Gas Gas1;
        public Comm.Gas Gas2;
        public MixturePair[] PairsOfMixtures;
        public double InitialPause;
        public double OdorFlowDuration;
        public bool WaitForPID;
        public double DMSSniffingDelay;
        //public int PIDReadingInterval;

        public int OdorFlowDurationMs => (int)(OdorFlowDuration * 1000);
        public Dictionary<string, string> Params => new()
        {
            { "sniffer", Sniffer.ToString() },
            { "fresh", FreshAirFlow.ToString() },
            { "practice_flow", PracticeOdorFlow.ToString() },
            { "test_flow", TestOdorFlow.ToString() },
            { "gas1", Gas1.ToString() },
            { "gas2", Gas2.ToString() },
            { "pause", InitialPause.ToString() },
            { "flow", OdorFlowDuration.ToString() },
            { "use_pid", WaitForPID.ToString() },
            { "dms_sniffing_delay", DMSSniffingDelay.ToString("F2") },
        };

        public Settings()
        {
            var settings = Properties.Settings.Default;

            Sniffer = (GasSniffer)Enum.Parse(typeof(GasSniffer), settings.Test_CMP_Sniffer);
            FreshAirFlow = settings.Test_CMP_FreshAirFlow;
            PracticeOdorFlow = settings.Test_CMP_PracticeOdorFlow;
            TestOdorFlow = settings.Test_CMP_TestOdorFlow;
            Gas1 = (Comm.Gas)settings.Setup_Gas1;
            Gas2 = (Comm.Gas)settings.Setup_Gas2;
            PairsOfMixtures = ParsePairsOfMixtures(settings.Test_CMP_Mixtures, out string _);
            InitialPause = settings.Test_CMP_InitialPause;
            OdorFlowDuration = settings.Test_CMP_OdorFlowDuration;
            WaitForPID = settings.Test_CMP_WaitForPID;
            DMSSniffingDelay = settings.Test_CMP_DMSSniffingDelay;
            //PIDReadingInterval = settings.Test_OP_PIDReadingInterval;
        }

        public void Save()
        {
            var settings = Properties.Settings.Default;

            settings.Test_CMP_Sniffer = Sniffer.ToString();
            settings.Test_CMP_FreshAirFlow = FreshAirFlow;
            settings.Test_CMP_PracticeOdorFlow = PracticeOdorFlow;
            settings.Test_CMP_TestOdorFlow = TestOdorFlow;
            settings.Test_CMP_Mixtures = SerializeMixtures();
            settings.Test_CMP_InitialPause = InitialPause;
            settings.Test_CMP_OdorFlowDuration = OdorFlowDuration;
            settings.Test_CMP_WaitForPID = WaitForPID;
            settings.Test_CMP_DMSSniffingDelay = DMSSniffingDelay;
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
