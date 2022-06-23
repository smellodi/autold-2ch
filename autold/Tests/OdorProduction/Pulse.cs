using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Olfactory.Tests.OdorProduction
{
    /// <summary>
    /// Describes a single channel pulse
    /// </summary>
    public class ChannelPulse
    {
        public int ID { get; }

        /// <summary>
        /// ml/min
        /// </summary>
        public double Flow { get; }

        /// <summary>
        /// ms; if saet to 0, then "Odor flow" value will be used 
        /// </summary>
        public int Duration { get; }

        /// <summary>
        /// ms
        /// </summary>
        public int Delay { get; } = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">1 or 2</param>
        /// <param name="flow">ml/min</param>
        /// <param name="duration">ms</param>
        /// <param name="delay">ms</param>
        public ChannelPulse(int id, double flow, int duration = 0, int delay = 0)
        {
            ID = id;
            Flow = flow;
            Duration = duration;
            Delay = delay;
        }

        public override string ToString()
        {
            return $"{ID}{Pulse.DELIM_EXPRESSION}"
                + (Delay > 0 ? $"[{Delay}]" : "")
                + (Duration > 0 ? $"{Flow}{Pulse.DELIM_BY}{Duration}" : Flow.ToString());
        }
    }

    /// <summary>
    /// Describes one or two channel(s) pulse
    /// </summary>
    public class Pulse
    {
        public static readonly char[] DELIM_LIST = new char[] { ' ', '\n' };
        public static readonly char DELIM_CHANNELS = ',';
        public static readonly char DELIM_EXPRESSION = '=';
        public static readonly char DELIM_BY = 'x';

        /// <summary>
        /// Channel A, can be null
        /// </summary>
        public ChannelPulse Channel1 { get; private set; }
        /// <summary>
        /// Channel B, can be null
        /// </summary>
        public ChannelPulse Channel2 { get; private set; }

        /// <summary>
        /// Valves affected by this pulse
        /// </summary>
        public Comm.MFC.ValvesOpened Valves =>
            (Channel1 != null ? Comm.MFC.ValvesOpened.Valve1 : Comm.MFC.ValvesOpened.None) |
            (Channel2 != null ? Comm.MFC.ValvesOpened.Valve2 : Comm.MFC.ValvesOpened.None);

        public static int ChannelDuration(ChannelPulse channel, int defaultDuration)
        {
            return channel == null ? 0 : (channel.Duration > 0 ? channel.Duration : defaultDuration);
        }

        /// <summary>
        /// Create a pulse
        /// </summary>
        /// <param name="input">Textual pulse description consisting of one or two sub-expressions of the form "channel=1|2"=["delay=ms"]"flow=ml/min"x"duration=ms" separated by comma. Example: '1=4x200,2=[100]4x100'</param>
        public Pulse(string input)
        {
            string[] channels = input.Split(DELIM_CHANNELS);
            if (channels.Length < 1 || channels.Length > 2)
            {
                throw new ArgumentException(string.Format(Utils.L10n.T("PulseInvalidNumberOfChannels"), input));
            }

            bool hasDelay = false;

            foreach (string channel in channels)
            {
                string[] p = channel.Split(DELIM_EXPRESSION);
                if (p.Length != 2)
                {
                    throw new ArgumentException(string.Format(Utils.L10n.T("PulseInvalidChannelExpression"), channel));
                }

                if (!int.TryParse(p[0], out int channelID) || (channelID != 1 && channelID != 2))
                {
                    throw new ArgumentException(string.Format(Utils.L10n.T("PulseInvalidChannelID"), p[0]));
                }

                Regex regex = new Regex(@"^(\[(?<delay>[0-9]+)\])?(?<flow>[0-9\.]+)(x(?<duration>[0-9]+))?$");
                var match = regex.Match(p[1]);

                if (!match.Success || match.Groups.Count < 1)
                {
                    throw new ArgumentException(string.Format(Utils.L10n.T("PulseInvalidChannelDescription"), p[0], p[1]));
                }

                var delayGroup = match.Groups["delay"];
                var durationGroup = match.Groups["duration"];
                var flowGroup = match.Groups["flow"];

                int delay = delayGroup.Success ? int.Parse(delayGroup.Value) : 0;
                double flow = double.Parse(flowGroup.Value);
                int duration = durationGroup.Success ? int.Parse(durationGroup.Value) : 0;

                if (delay > 0)
                {
                    if (hasDelay)
                    {
                        throw new ArgumentException(string.Format(Utils.L10n.T("PulseOnlyOneChannelCanDelay"), input));
                    }
                    hasDelay = true;
                }

                if (channelID == 1 && Channel1 == null)
                {
                    Channel1 = new ChannelPulse(channelID, flow, duration, delay);
                }
                else if (channelID == 2 && Channel2 == null)
                {
                    Channel2 = new ChannelPulse(channelID, flow, duration, delay);
                }
                else
                {
                    throw new ArgumentException(string.Format(Utils.L10n.T("PulseSameChannelTwice"), input));
                }
            }
        }

        public override string ToString()
        {
            var pulses = new List<string>();
            if (Channel1 != null)
            {
                pulses.Add(Channel1.ToString());
            }
            if (Channel2 != null)
            {
                pulses.Add(Channel2.ToString());
            }
            return string.Join(DELIM_CHANNELS, pulses);
        }

        public int WholeDuration(int defaultDuration)
        {
            var duration1 = ChannelDuration(Channel1, defaultDuration) + Channel1?.Delay ?? 0;
            var duration2 = ChannelDuration(Channel2, defaultDuration) + Channel2?.Delay ?? 0;
            return Math.Max(duration1, duration2);
        }
    }
}
