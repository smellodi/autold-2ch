using System;

namespace Olfactory.Comm
{
    internal class MFCEmulator
    {
        public static MFCEmulator Instance => _instance ??= new();

        public double FreshAirFlowRate => _massFlowA;
        public double OdorFlowRate => _massFlowB;

        public MFC.OdorFlowsTo OdorDirection => _odorDirection;

        public string EmulateReading(char channel)
        {
            /* Uncomment this if error emulation is desired
            if (rnd.NextDouble() < 0.0002)
            {
                return "";
            }*/

            if (channel == 'Z')
            {
                return string.Join(' ',
                    'Z',
                    OdorDirection.HasFlag(MFC.OdorFlowsTo.System) ? 1 : 0,
                    OdorDirection.HasFlag(MFC.OdorFlowsTo.User) ? 1 : 0
                );
            }

            var pressure = channel == 'A' ? _pressureA : _pressureB;
            var massFlow = channel == 'A' ? _massFlowA : _massFlowB;
            var volFlow = channel == 'A' ? _volFlowA : _volFlowB;

            return string.Join(' ',
                channel.ToString(),                         // channel
                (pressure + e(15)).ToString(),              // Absolute pressure
                (24.74 + e(0.3)).ToString("F2"),            // Temp
                (volFlow + e(0.05)).ToString("F5"),         // Volumentric flow
                (massFlow + e(0.05)).ToString("F5"),        // Standart (Mass) Flow
                "+50.000",                                  // Setpoint
                "Air"                                       // Gas
            );
        }

        public void EmulateWriting(byte[] command)
        {
            /* Uncomment this if error emulation is desired
            if (rnd.NextDouble() < 0.0002)
            {
                throw new Exception("Simulating writing fault");
            }*/

            var cmd = System.Text.Encoding.Default.GetString(command);
            if (cmd.Length > 4)
            {
                MFC.Channel channel = (MFC.Channel)Enum.Parse(typeof(MFC.Channel), cmd[0].ToString(), true);
                string cmdID = cmd[1].ToString();
                if (cmdID == MFC.CMD_SET)
                {
                    double value = double.Parse(cmd.Substring(2, command.Length - 2));
                    switch (channel)
                    {
                        case MFC.Channel.A:
                            _massFlowA = value;
                            break;
                        case MFC.Channel.B:
                            _massFlowB = value;
                            break;
                        case MFC.Channel.Z:
                            _odorDirection = (MFC.OdorFlowsTo)value;
                            break;
                        default: break;
                    }
                }
            }
        }

        // Internal

        static MFCEmulator _instance;

        Random _rnd = new Random((int)DateTime.Now.Ticks);
        int _pressureA = 1200;
        int _pressureB = 1800;
        double _massFlowA = 1.0;
        double _massFlowB = 0.02;
        double _volFlowA = .05;
        double _volFlowB = .05;
        MFC.OdorFlowsTo _odorDirection = MFC.OdorFlowsTo.Waste;

        private MFCEmulator() { }

        // emulates measurement inaccuracy
        private double e(double amplitude) => (_rnd.NextDouble() - 0.5) * 2 * amplitude;
        private int e(int amplitude) => _rnd.Next(-amplitude, amplitude);
    }
}
