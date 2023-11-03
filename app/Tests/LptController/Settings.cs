using System;
using System.Linq;
using System.Collections.Generic;
using AutOlD2Ch.Tests.Common;

namespace AutOlD2Ch.Tests.LptController;

public class Settings
{
    public const int MIN_FLOW_DURATION = 10; // ms

    public int LptPort;
    public double FreshAir;
    public double OdorFlowDuration;
    public Dictionary<int, Pulse> Pulses;
    public int PIDReadingInterval;

    public int OdorFlowDurationMs => (int)(OdorFlowDuration * 1000);

    public Settings()
    {
        var settings = Properties.Settings.Default;

        LptPort = settings.Test_LC_LptPort;
        FreshAir = settings.Test_LC_FreshAir;
        OdorFlowDuration = settings.Test_LC_OdorFlowDuration;
        Pulses = ParsePulses(settings.Test_LC_Pulses, out string _);
        PIDReadingInterval = settings.Test_LC_PIDReadingInterval;
    }

    public void Save()
    {
        var settings = Properties.Settings.Default;

        settings.Test_LC_LptPort = LptPort;
        settings.Test_LC_FreshAir = FreshAir;
        settings.Test_LC_Pulses = string.Join(Pulse.DELIM_LIST[0], Pulses.Select(pulse => pulse.ToString()));
        settings.Test_LC_OdorFlowDuration = OdorFlowDuration;
        settings.Test_LC_PIDReadingInterval = PIDReadingInterval;

        settings.Save();
    }

    public string SerializePulses()
    {
        return string.Join(Pulse.DELIM_LIST[1], Pulses.Select(pulse => pulse.ToString()));
    }

    public static Dictionary<int, Pulse> ParsePulses(string input, out string error)
    {
        var pulsesStr = input
            .Split(Pulse.DELIM_EXPRESSION_WITH_MARKER)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p));

        var pulses = new Dictionary<int, Pulse>();

        foreach (var pulseStr in pulsesStr)
        {
            try
            {
                var mp = pulseStr.Split(Pulse.DELIM_MARKER);
                pulses.Add(int.Parse(mp[0]), new Pulse(mp[1]));
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return pulses;
            }
        }

        error = null;
        return pulses;
    }
}
