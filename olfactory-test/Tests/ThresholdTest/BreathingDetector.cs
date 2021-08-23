using System;
using System.Collections.Generic;
using System.Text;

namespace Olfactory.Tests.ThresholdTest
{
    public class BreathingDetector
    {
        public enum Stage
        {
            None,
            Inhale,
            Exhale
        }

        public Stage BreathingStage { get; private set; } = Stage.None;

        public bool Feed(double value)
        {
            bool isStageChanged = false;

            // Here comes inhale start detection

            return isStageChanged;
        }
    }
}
