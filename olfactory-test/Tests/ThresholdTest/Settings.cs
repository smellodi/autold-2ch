using System.Linq;
using System.Collections.Generic;

namespace Olfactory.Tests.ThresholdTest
{
    public class Settings
    {
        public double FreshAir;
        public double[] PPMs;
        public int OdorPreparationDuration;
        public double PenSniffingDuration;
        public int PIDReadingInterval;
        public int RecognitionsInRow;
        public int TurningPoints;
        public int TurningPointsToCount;
        public double FamiliarizationDuration; // seconds
        public bool UseFeedbackLoopToReachLevel;
        public bool UseFeedbackLoopToKeepLevel;
        public Procedure.PenPresentationStart FlowStart;

        public Settings()
        {
            var settings = Properties.Settings.Default;

            FreshAir = settings.Test_TT_FreshAir;
            OdorPreparationDuration = settings.Test_TT_OdorPreparationDuration;
            PenSniffingDuration = settings.Test_TT_PenSniffingDuration;
            PIDReadingInterval = settings.Test_TT_PIDReadingInterval;
            RecognitionsInRow = settings.Test_TT_RecognitionInRow;
            TurningPoints = settings.Test_TT_TurningPoints;
            TurningPointsToCount = settings.Test_TT_TurningPointsToCount;
            FamiliarizationDuration = settings.Test_TT_FamiliarizationDuration;
            UseFeedbackLoopToReachLevel = settings.Test_TT_UseFeedbackLoopToReachLevel;
            UseFeedbackLoopToKeepLevel = settings.Test_TT_UseFeedbackLoopToKeepLevel;
            FlowStart = (Procedure.PenPresentationStart)settings.Test_TT_FlowStart;

            List<double> odorQuantities = new List<double>();
            foreach (var q in settings.Test_TT_PPMs)
            {
                odorQuantities.Add(double.Parse(q));
            }
            PPMs = odorQuantities.ToArray();
        }

        public void Save()
        {
            var settings = Properties.Settings.Default;

            settings.Test_TT_FreshAir = FreshAir;
            settings.Test_TT_OdorPreparationDuration = OdorPreparationDuration;
            settings.Test_TT_PenSniffingDuration = PenSniffingDuration;
            settings.Test_TT_PIDReadingInterval = PIDReadingInterval;
            settings.Test_TT_RecognitionInRow = RecognitionsInRow;
            settings.Test_TT_TurningPoints = TurningPoints;
            settings.Test_TT_TurningPointsToCount = TurningPointsToCount;
            settings.Test_TT_FamiliarizationDuration = FamiliarizationDuration;
            settings.Test_TT_UseFeedbackLoopToReachLevel = UseFeedbackLoopToReachLevel;
            settings.Test_TT_UseFeedbackLoopToKeepLevel = UseFeedbackLoopToKeepLevel;
            settings.Test_TT_FlowStart = settings.Test_TT_FlowStart;

            settings.Test_TT_PPMs.Clear();
            settings.Test_TT_PPMs.AddRange(PPMs.Select(ppm => ppm.ToString()).ToArray());

            settings.Save();
        }
    }
}
