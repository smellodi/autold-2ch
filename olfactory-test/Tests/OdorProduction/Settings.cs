using System.Linq;
using System.Collections.Generic;

namespace Olfactory.Tests.OdorProduction
{
    public class Settings
    {
        public double FreshAir;
        public double[] OdorQuantities;
        public int InitialPause;
        public int OdorFlowDuration;
        public int FinalPause;
        public int PIDReadingInterval;
        public bool Valve2ToUser;
        public bool UseFeedbackLoopToReachLevel;
        public bool UseFeedbackLoopToKeepLevel;

        public Settings()
        {
            var settings = Properties.Settings.Default;

            FreshAir = settings.Test_OP_FreshAir;
            InitialPause = settings.Test_OP_InitialPause;
            OdorFlowDuration = settings.Test_OP_OdorFlowDuration;
            FinalPause = settings.Test_OP_FinalPause;
            PIDReadingInterval = settings.Test_OP_PIDReadingInterval;
            Valve2ToUser = settings.Test_OP_Valve2User;
            UseFeedbackLoopToReachLevel = settings.Test_OP_UseFeedbackLoopToReachLevel;
            UseFeedbackLoopToKeepLevel = settings.Test_OP_UseFeedbackLoopToKeepLevel;

            List<double> odorQuantities = new List<double>();
            foreach (var q in settings.Test_OP_OdorQuantities)
            {
                odorQuantities.Add(double.Parse(q));
            }
            OdorQuantities = odorQuantities.ToArray();
        }

        public void Save()
        {
            var settings = Properties.Settings.Default;

            settings.Test_OP_FreshAir = FreshAir;
            settings.Test_OP_InitialPause = InitialPause;
            settings.Test_OP_OdorFlowDuration = OdorFlowDuration;
            settings.Test_OP_FinalPause = FinalPause;
            settings.Test_OP_PIDReadingInterval = PIDReadingInterval;
            settings.Test_OP_Valve2User = Valve2ToUser;
            settings.Test_OP_UseFeedbackLoopToReachLevel = UseFeedbackLoopToReachLevel;
            settings.Test_OP_UseFeedbackLoopToKeepLevel = UseFeedbackLoopToKeepLevel;

            settings.Test_OP_OdorQuantities.Clear();
            settings.Test_OP_OdorQuantities.AddRange(OdorQuantities.Select(q => q.ToString()).ToArray());

            settings.Save();
        }
    }
}
