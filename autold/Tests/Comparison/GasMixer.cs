using System.Collections.Generic;
using Olfactory2Ch.Comm;
using Olfactory2Ch.Tests.Common;

namespace Olfactory2Ch.Tests.Comparison
{
    internal class GasMixer
    {
        public static Pulse ToPulse(MixturePair pair, int mixID, double baseOdorFlow, int flowDuration, Gas gas1, Gas gas2)
        {
            var mixture = mixID == 0 ? pair.Mix1 : pair.Mix2;

            return mixture switch
            {
                Mixture.Odor1 => Mix(baseOdorFlow, flowDuration, gas1, gas2, 1.0),
                Mixture.Odor2 => Mix(baseOdorFlow, flowDuration, gas1, gas2, 0.0),
                _ => Mix(baseOdorFlow, flowDuration, gas1, gas2, 0.01 * int.Parse(mixture.ToString().Substring(3)))
            };
        }

        // Internal

        static readonly Dictionary<Gas, double> GAS_INTENSITY_WEIGHT = new() {
            { Gas.nButanol, 1.0 },
            { Gas.IPA, 2.0 },
        };

        private static Pulse Mix(double baseOdorFlow, int flowDuration, Gas gas1, Gas gas2, double gas1Share)
        {
            double w1 = GAS_INTENSITY_WEIGHT[gas1];
            double w2 = GAS_INTENSITY_WEIGHT[gas2];
            double s1 = gas1Share;
            double s2 = 1.0 - gas1Share;

            List<ChannelPulse> channels = new List<ChannelPulse>();
            if (s1 > 0) channels.Add(new ChannelPulse(1, baseOdorFlow * s1 * w1, flowDuration));
            if (s2 > 0) channels.Add(new ChannelPulse(2, baseOdorFlow * s2 * w2, flowDuration));

            return new Pulse(channels.ToArray());
        }
    }
}
