using System;
using System.Linq;
using System.Collections.Generic;
using AutOlD2Ch.Tests.Common;

namespace AutOlD2Ch.Tests.OdorProduction
{
    public class Settings
    {
        public const int MIN_FLOW_DURATION = 10; // ms

        public double FreshAir;
        public Pulse[] Pulses;
        public int InitialPause;
        public double OdorFlowDuration;
        public int FinalPause;
        public int PIDReadingInterval;
        public bool UseValveTimer;
        public bool ManualFlowStop;
        public bool RandomizeOrder;

        public int OdorFlowDurationMs => (int)(OdorFlowDuration * 1000);

        public Settings()
        {
            var settings = Properties.Settings.Default;

            FreshAir = settings.Test_OP_FreshAir;
            Pulses = ParsePulses(settings.Test_OP_Pulses, out string _);
            InitialPause = settings.Test_OP_InitialPause;
            OdorFlowDuration = settings.Test_OP_OdorFlowDuration;
            FinalPause = settings.Test_OP_FinalPause;
            PIDReadingInterval = settings.Test_OP_PIDReadingInterval;
            UseValveTimer = settings.Test_OP_UseValveTimer;
            ManualFlowStop = settings.Test_OP_ManualFlowStop;
            RandomizeOrder = settings.Test_OP_RandomizeOrder;
        }

        public void Save()
        {
            var settings = Properties.Settings.Default;

            settings.Test_OP_FreshAir = FreshAir;
            settings.Test_OP_Pulses = string.Join(Pulse.DELIM_LIST[0], Pulses.Select(pulse => pulse.ToString()));
            settings.Test_OP_InitialPause = InitialPause;
            settings.Test_OP_OdorFlowDuration = OdorFlowDuration;
            settings.Test_OP_FinalPause = FinalPause;
            settings.Test_OP_PIDReadingInterval = PIDReadingInterval;
            settings.Test_OP_UseValveTimer = UseValveTimer;
            settings.Test_OP_ManualFlowStop = ManualFlowStop;
            settings.Test_OP_RandomizeOrder = RandomizeOrder;

            settings.Save();
        }

        public string SerializePulses()
        {
            return string.Join(Pulse.DELIM_LIST[1], Pulses.Select(pulse => pulse.ToString()));
        }

        public static Pulse[] ParsePulses(string input, out string error)
        {
            var pulsesStr = input.Split(Pulse.DELIM_LIST);

            var pulses = new List<Pulse>();

            foreach (var pulseStr in pulsesStr)
            {
                try
                {
                    Pulse pulse = new Pulse(pulseStr);
                    pulses.Add(pulse);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return null;
                }
            }

            error = null;
            return pulses.ToArray();
        }
    }
}
