using System;
using System.Collections.Generic;
using Olfactory.Tests.Common;

namespace Olfactory.Tests.Comparison
{
    internal class GasMixer
    {
        public static Pulse ToPulse(MixturePair pair, int mixID, double baseOdorFlow, int flowDuration, Gas gas1, Gas gas2)
        {
            var mixture = mixID == 0 ? pair.Mix1 : pair.Mix2;

            return mixture switch
            {
                Mixture.Odor1 => new Pulse(new ChannelPulse[] { new ChannelPulse(1, baseOdorFlow, flowDuration) }),
                Mixture.Odor2 => new Pulse(new ChannelPulse[] { new ChannelPulse(2, baseOdorFlow, flowDuration) }),
                Mixture.Mix50 => Mix(baseOdorFlow, flowDuration, gas1, gas2, 0.5),
                _ => throw new Exception($"Gas '{mixture}' has no implemntation in pulse creation procedure")
            };
        }

        public static double GetExpectedPID(Gas gas1, double gas1Flow, Gas gas2, double gas2Flow)
        {
            double k1 = MV_PER_MLMIN[gas1];
            double k2 = MV_PER_MLMIN[gas2];
            return MV_BASELINE + (k1 * gas1Flow + k2 * gas2Flow);
        }

        // Internal

        static readonly double MV_BASELINE = 56;
        static readonly Dictionary<Gas, double> MV_PER_MLMIN = new() {
            { Gas.nButanol, 20 },
            { Gas.IPA, 35 },
        };

        private static Pulse Mix(double baseOdorFlow, int flowDuration, Gas gas1, Gas gas2, double gas1Share)
        {
            // TODO: more complex mixing must be implemented, where a gas type is taken into account

            return new Pulse(new ChannelPulse[]
            {
                new ChannelPulse(1, baseOdorFlow * gas1Share, flowDuration),
                new ChannelPulse(2, baseOdorFlow * (1.0 - gas1Share), flowDuration)
            });
        }
    }
}
