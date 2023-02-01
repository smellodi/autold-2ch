using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Olfactory.Tests.Common
{
    /// <summary>
    /// Describes a single channel pulse
    /// </summary>
    public class ChannelPulse
    {
        /// <summary>
        /// 1 or 2
        /// </summary>
        public int ID { get; }

        /// <summary>
        /// ml/min
        /// </summary>
        public double Flow { get; }

        /// <summary>
        /// ms
        /// </summary>
        public int Delay { get; } = 0;

        /// <summary>
        /// The corresponding valve
        /// </summary>
        public Comm.MFC.ValvesOpened Valve => ID == 1 ? Comm.MFC.ValvesOpened.Valve1 : Comm.MFC.ValvesOpened.Valve2;

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
            Delay = delay;

            _duration = duration;
        }

        /// <summary>
        /// Returns duration in ms. If it was set to 0 in the constructor, 
        /// then the default duration provided as an argument will be used 
        /// </summary>
        /// <param name="defaultDuration">Default duration: must be "Odor flow", <see cref="Settings.OdorFlowDuration"/></param>
        /// <returns>Duration in ms</returns>
        public int GetDuration(int defaultDuration)
        {
            return _duration > 0 ? _duration : defaultDuration;
        }

        /// <summary>
        /// Returns delay + duration in ms. Note that if the duration was set to 0 in the constructor, 
        /// then the default duration provided as an argument will be used 
        /// </summary>
        /// <param name="defaultDuration">Default duration: must be "Odor flow", <see cref="Settings.OdorFlowDuration"/></param>
        /// <returns>Timestamp in ms</returns>
        public int GetFinishedTimestamp(int defaultDuration)
        {
            return Delay + GetDuration(defaultDuration);
        }

        public override string ToString()
        {
            return $"{ID}{Pulse.DELIM_EXPRESSION}"
                + (Delay > 0 ? $"[{Delay}]" : "")
                + (_duration > 0 ? $"{Flow}{Pulse.DELIM_BY}{_duration}" : Flow.ToString());
        }

        // Internal

        int _duration;
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

        /// <summary>
        /// Create a pulse from a definition string
        /// </summary>
        /// <param name="input">Pulse expression consisting of one or two sub-expressions of the form
        /// <code>"channel=1|2"=[["delay=ms"]]"flow=ml/min"[x"duration=ms"]</code> separated by comma.
        /// Note that delay and duration are optional: the default value is 0 for delay, and duration will be set to
        /// <see cref="Settings.OdorFlowDuration"/> is not specified in the expression
        /// Examples:
        /// <list type="bullet">
        /// <item>'1=4' - the first channels is used only, and the pulse duration is describes in <see cref="Settings.OdorFlowDuration"/></item>
        /// <item>'1=4x200,2=[100]4x100' - the first channels starts immediately and lasts 200ms, the second channels delays 100ms and lasts 100ms</item>
        /// </list> 
        /// </param>
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
                var (channelID, delay, flow, duration) = Parse(channel);

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

        public Pulse(ChannelPulse[] channels)
        {
            if (channels.Length < 1 || channels.Length > 2)
            {
                throw new ArgumentException(string.Format(Utils.L10n.T("PulseInvalidNumberOfChannels"), channels.Length));
            }

            bool hasDelay = false;

            foreach (var channel in channels)
            {
                if (channel.Delay > 0)
                {
                    if (hasDelay)
                    {
                        throw new ArgumentException(string.Format(Utils.L10n.T("PulseOnlyOneChannelCanDelay"), channel));
                    }
                    hasDelay = true;
                }

                if (channel.ID == 1 && Channel1 == null)
                {
                    Channel1 = channel;
                }
                else if (channel.ID == 2 && Channel2 == null)
                {
                    Channel2 = channel;
                }
                else
                {
                    throw new ArgumentException(string.Format(Utils.L10n.T("PulseSameChannelTwice"), channel));
                }
            }
        }

        public int GetDuration(int defaultDuration)
        {
            var duration1 = Channel1 != null ? Channel1.Delay + Channel1.GetDuration(defaultDuration) : 0;
            var duration2 = Channel2 != null ? Channel2.Delay + Channel2.GetDuration(defaultDuration) : 0;
            return Math.Max(duration1, duration2);
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

        // Internal
        (int, int, double, int) Parse(string channel)
        {
            string[] p = channel.Split(DELIM_EXPRESSION);
            if (p.Length != 2)
            {
                throw new ArgumentException(string.Format(Utils.L10n.T("PulseInvalidChannelExpression"), channel));
            }

            if (!int.TryParse(p[0], out int channelID) || channelID != 1 && channelID != 2)
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

            return (
                channelID,
                delayGroup.Success ? int.Parse(delayGroup.Value) : 0,
                double.Parse(flowGroup.Value),
                durationGroup.Success ? int.Parse(durationGroup.Value) : 0
            );
        }
    }
}
