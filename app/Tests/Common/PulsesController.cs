using System;
using System.Collections.Generic;
using System.Linq;

namespace AutOlD2Ch.Tests.Common;

/// <summary>
/// Generates a list of pulse state changes, and fires each change event at its proper time
/// </summary>
public class PulsesController : IDisposable
{
    public class PulseStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Channel that starts flowing
        /// </summary>
        public ChannelPulse[] StartingChannels { get; }
        /// <summary>
        /// Channel that continues to flow (can be null)
        /// </summary>
        public ChannelPulse[] OngoingChannels { get; }
        /// <summary>
        /// Indicates that the channels that start flowing has delay, and no more <see cref="PulseStateChanged"/> events will be fired
        /// </summary>
        public bool IsLast { get; set; } = false;

        public PulseStateChangedEventArgs(ChannelPulse? startingChannel, ChannelPulse? ongoingChannel = null, bool isLast = false)
        {
            StartingChannels = startingChannel != null ? new ChannelPulse[] { startingChannel } : Array.Empty<ChannelPulse>();
            OngoingChannels = ongoingChannel != null ? new ChannelPulse[] { ongoingChannel } : Array.Empty<ChannelPulse>();
            IsLast = isLast;
        }
        public PulseStateChangedEventArgs(ChannelPulse[]? startingChannels, ChannelPulse[]? ongoingChannels = null)
        {
            StartingChannels = startingChannels ?? Array.Empty<ChannelPulse>();
            OngoingChannels = ongoingChannels ?? Array.Empty<ChannelPulse>();
        }

        public override string ToString()
        {
            var result = "";
            if (StartingChannels.Length > 0)
            {
                result += " START " + string.Join(", ", StartingChannels.Select(evt => $"{evt.ID}"));
            }
            if (OngoingChannels.Length > 0)
            {
                result += " CONT " + string.Join(", ", OngoingChannels.Select(evt => $"{evt.ID}"));
            }
            if (IsLast)
            {
                result += " FINISHED";
            }
            return result;
        }
    }

    public event EventHandler<PulseStateChangedEventArgs>? PulseStateChanged;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pulse">Pulse to control</param>
    /// <param name="defaultFlowDuration">Default channel duration, ms</param>
    public PulsesController(Pulse pulse, int defaultFlowDuration)
    {
        _pulse = pulse;
        _defaultPulseDuration = defaultFlowDuration;
    }

    /// <summary>
    /// Start the flow procedure
    /// </summary>
    public void Run()
    {
        var events = new List<ChannelEvent>();
        if (_pulse.Channel1 != null)
        {
            events.Add(new ChannelEvent(_pulse.Channel1.Delay, _pulse.Channel1, ChannelEvent.EventType.Start));
            events.Add(new ChannelEvent(_pulse.Channel1.GetFinishedTimestamp(_defaultPulseDuration), _pulse.Channel1, ChannelEvent.EventType.End));
        }
        if (_pulse.Channel2 != null)
        {
            events.Add(new ChannelEvent(_pulse.Channel2.Delay, _pulse.Channel2, ChannelEvent.EventType.Start));
            events.Add(new ChannelEvent(_pulse.Channel2.GetFinishedTimestamp(_defaultPulseDuration), _pulse.Channel2, ChannelEvent.EventType.End));
        }

        var pulseEvents = PulseEvent.CreateSequence(events.ToArray());

        // Fire the first event
        PulseStateChanged?.Invoke(this, pulseEvents[0].ToStateChange());

        // Schedule the second event (there are always at least 2 events: start and stop of the same channel)
        _runner = Utils.DispatchOnce.Do(pulseEvents[1].Interval, () =>
        {
            PulseStateChanged?.Invoke(this, pulseEvents[1].ToStateChange());
        });

        // Schedule the rest of events (up to 4 events may exist for two channels)
        for (int i = 2; i < pulseEvents.Length; i++)
        {
            var evt = pulseEvents[i];
            _runner?.Then(evt.Interval, () =>
            {
                PulseStateChanged?.Invoke(this, evt.ToStateChange());
            });
        }
    }

    public void Terminate()
    {
        _runner?.Stop();
    }

    public void Dispose()
    {
        _runner?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    class ChannelEvent
    {
        public enum EventType
        {
            Start,
            End
        }
        public int Delay { get; }
        public ChannelPulse Channel { get; }
        public EventType Type { get; }
        public ChannelEvent(int delay, ChannelPulse channel, EventType type)
        {
            Delay = delay;
            Channel = channel;
            Type = type;
        }
    }

    class PulseEvent
    {
        /// <summary>
        /// Delay from the pulse onset time
        /// </summary>
        public int Delay { get; }
        /// <summary>
        /// Interval from the previous event, seconds
        /// </summary>
        public double Interval => (double)(Delay - (Previous?.Delay ?? 0)) / 1000;
        public ChannelEvent[] Starting => _starting.ToArray();
        public ChannelEvent[] Ending => _ending.ToArray();
        public PulseEvent? Previous => _previousEvent;
        public PulseEvent? Next { get; set; }

        public PulseEvent(PulseEvent? previousEvent, ChannelEvent evt)
        {
            _previousEvent = previousEvent;
            Delay = evt.Delay;
            Add(evt);
        }
        public void Add(ChannelEvent evt)
        {
            if (evt.Type == ChannelEvent.EventType.Start)
            {
                _starting.Add(evt);
            }
            else
            {
                _ending.Add(evt);
            }
        }

        public PulseStateChangedEventArgs ToStateChange()
        {
            // To find the ongoing/continuing channels, we need to start from the first pulse event
            // and go up to the current one, memorizing channels that have started but not ended yet
            HashSet<ChannelPulse> activePulses = new();

            var firstEvent = this;
            while (firstEvent.Previous != null) firstEvent = firstEvent.Previous;   // get the first event
            while (firstEvent != this && firstEvent != null)
            {
                foreach (var evt in firstEvent.Starting)
                {
                    activePulses.Add(evt.Channel);
                }
                foreach (var evt in firstEvent.Ending)
                {
                    activePulses.Remove(evt.Channel);
                }
                firstEvent = firstEvent.Next;
            }

            if (firstEvent != null)
            {
                foreach (var evt in firstEvent.Ending)
                {
                    activePulses.Remove(evt.Channel);
                }
            }

            var result = new PulseStateChangedEventArgs(
                _starting.Select(evt => evt.Channel).ToArray(),
                activePulses.ToArray()
            );
            result.IsLast = Next == null;

            return result;
        }

        public override string ToString()
        {
            var result = $"{Delay}";
            if (_starting.Count > 0)
            {
                result += " START " + string.Join(", ", _starting.Select(evt => $"{evt.Channel.ID}"));
            }
            if (_ending.Count > 0)
            {
                result += " END " + string.Join(", ", _ending.Select(evt => $"{evt.Channel.ID}"));
            }
            return result;
        }

        public static PulseEvent[] CreateSequence(ChannelEvent[] events)
        {
            var orderedEvents = events.OrderBy(evt => evt.Delay);

            List<PulseEvent> result = new();
            PulseEvent? currentEvent = null;

            foreach (var evt in orderedEvents)
            {
                if (currentEvent?.Delay == evt.Delay)
                {
                    currentEvent.Add(evt);
                    continue;
                }

                var previousEvent = currentEvent;
                currentEvent = new PulseEvent(previousEvent, evt);
                result.Add(currentEvent);

                if (previousEvent != null)
                {
                    previousEvent.Next = currentEvent;
                }
            }

            return result.ToArray();
        }

        // Internal

        readonly List<ChannelEvent> _starting = new();
        readonly List<ChannelEvent> _ending = new();
        readonly PulseEvent? _previousEvent;
    }

    readonly Pulse _pulse;
    readonly int _defaultPulseDuration;

    Utils.DispatchOnce? _runner;
}
