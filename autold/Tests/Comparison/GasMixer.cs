using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text.Json;
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

        [Serializable]
        public class GasProp
        {
            public double Weight { get; set; }
            public override string ToString() => $"w = {Weight}";
        }

        static readonly Dictionary<Gas, GasProp> GasProperties = new() {
            { Gas.nButanol, new GasProp() { Weight = 1.0 } },
            { Gas.IPA, new GasProp() { Weight = 2.0 } },
        };

        static GasMixer()
        {
            try
            {
                System.IO.StreamReader reader = new("Properties/GasProps.json");
                var gasPropsJson = reader.ReadToEnd();

                JsonSerializerOptions options = new() { ReadCommentHandling = JsonCommentHandling.Skip };
                var gasProps = (Dictionary<string, GasProp>)JsonSerializer.Deserialize(gasPropsJson.Trim(), typeof(Dictionary<string, GasProp>), options);

                foreach (var record in gasProps)
                {
                    if (Enum.TryParse(typeof(Gas), record.Key, out object gasObj) && gasObj is Gas gas)
                    {
                        if (GasProperties.ContainsKey(gas))
                        {
                            GasProperties[gas] = record.Value;
                            Debug.WriteLine($"{gas} PROPS: {record.Value}");
                        }
                        else
                        {
                            GasProperties.Add(gas, record.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static Pulse Mix(double baseOdorFlow, int flowDuration, Gas gas1, Gas gas2, double gas1Share)
        {
            double w1 = GasProperties[gas1].Weight;
            double w2 = GasProperties[gas2].Weight;
            double s1 = gas1Share;
            double s2 = 1.0 - gas1Share;

            List<ChannelPulse> channels = new List<ChannelPulse>();
            if (s1 > 0) channels.Add(new ChannelPulse(1, baseOdorFlow * s1 * w1, flowDuration));
            if (s2 > 0) channels.Add(new ChannelPulse(2, baseOdorFlow * s2 * w2, flowDuration));

            return new Pulse(channels.ToArray());
        }
    }
}
