using System;
using System.Linq;
using System.Collections.Generic;
using AutOlD2Ch.Tests.Common;

namespace AutOlD2Ch.Tests.LptController;

public class Settings
{
    public int LptPort;
    public string ComPort;
    public double FreshAir;
    public double OdorFlowDuration;
    public Dictionary<int, Pulse> Pulses;
    public int PIDReadingInterval;

    public int OdorFlowDurationMs => (int)(OdorFlowDuration * 1000);

    public Settings()
    {
        var settings = Properties.Settings.Default;

        LptPort = settings.Test_LC_LptPort;
        ComPort = settings.Test_LC_ComPort;
        FreshAir = settings.Test_LC_FreshAir;
        OdorFlowDuration = settings.Test_LC_OdorFlowDuration;
        Pulses = ParsePulses(settings.Test_LC_Pulses, out string _);
        PIDReadingInterval = settings.Test_LC_PIDReadingInterval;
    }

    public void Save()
    {
        var settings = Properties.Settings.Default;

        settings.Test_LC_LptPort = LptPort;
        settings.Test_LC_ComPort = ComPort;
        settings.Test_LC_FreshAir = FreshAir;
        settings.Test_LC_Pulses = SerializePulses();
        settings.Test_LC_OdorFlowDuration = OdorFlowDuration;
        settings.Test_LC_PIDReadingInterval = PIDReadingInterval;

        settings.Save();
    }

    public string SerializePulses() => string.Join(
        Pulse.DELIM_EXPRESSION_WITH_MARKER, Pulses.Select(
            kv => $"{kv.Key}{Pulse.DELIM_MARKER} {kv.Value}"
        )
    );

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
