using System;
using Olfactory.Tests.Common;

namespace Olfactory.Tests.Comparison
{
    internal class GasMixer
    {
        public static Pulse ToPulse(MixturePair pair, int mixID, double baseOdorFlow, int flowDuration, Gas gas1, Gas gas2)
        {
            var channel = mixID == 0 ? pair.Mix1 : pair.Mix2;

            return channel switch
            {
                Mixture.Odor1 => new Pulse(new ChannelPulse[] { new ChannelPulse(1, baseOdorFlow, flowDuration) }),
                Mixture.Odor2 => new Pulse(new ChannelPulse[] { new ChannelPulse(2, baseOdorFlow, flowDuration) }),
                Mixture.Mix50 => Mix(baseOdorFlow, flowDuration, gas1, gas2, 0.5),
                _ => throw new Exception($"Gas '{channel}' has no implemntation in pulse creation procedure")
            };
        }

        // Internal

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
