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

            var input = System.Text.Encoding.Default.GetString(command);
            var cmds = input.Split(MFC.DATA_END);
            foreach (var cmd in cmds)
            {
                if (cmd.Length > 4)
                {
                    ExecuteCommand(cmd);
                }
            }
        }

        // Internal

        static MFCEmulator _instance;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);

        const int _pressureA = 1200;
        const int _pressureB = 1800;
        const double _volFlowA = .05;
        const double _volFlowB = .05;

        double _massFlowA = 1.0;
        double _massFlowB = 0.02;

        MFC.OdorFlowsTo _odorDirection = MFC.OdorFlowsTo.Waste;

        Utils.DispatchOnce _valve1ShortPulseTimer = null;
        Utils.DispatchOnce _valve2ShortPulseTimer = null;
        bool _isValve1InShortPulseMode = false;
        bool _isValve2InShortPulseMode = false;

        private MFCEmulator() { }

        // emulates measurement inaccuracy
        private double e(double amplitude) => (_rnd.NextDouble() - 0.5) * 2 * amplitude;
        private int e(int amplitude) => _rnd.Next(-amplitude, amplitude);

        private void ExecuteCommand(string cmd)
        {
            MFC.Channel channel = (MFC.Channel)Enum.Parse(typeof(MFC.Channel), cmd[0].ToString(), true);
            string cmdID = cmd[1].ToString();
            if (cmdID == MFC.CMD_SET)
            {
                var value = double.Parse(cmd[2..]);
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
            else if (cmdID == MFC.CMD_WRITE_REGISTER)
            {
                MFC.Register register = (MFC.Register)int.Parse(cmd.Substring(2, 1));
                var value = int.Parse(cmd[4..]);

                switch (register)
                {
                    case MFC.Register.HOLD_0:
                        _isValve1InShortPulseMode = value < 255;
                        break;
                    case MFC.Register.HOLD_1:
                        _isValve2InShortPulseMode = value < 255;
                        break;
                    case MFC.Register.PULL_IN_0:
                        if (_isValve1InShortPulseMode && value > 0 && value <= 0xFFFF && _valve1ShortPulseTimer == null)
                        {
                            _valve1ShortPulseTimer = Utils.DispatchOnce.Do((double)value / 1000, () =>
                            {
                                _valve1ShortPulseTimer = null;
                            });
                        }
                        break;
                    case MFC.Register.PULL_IN_1:
                        if (_isValve2InShortPulseMode && value > 0 && value <= 0xFFFF && _valve2ShortPulseTimer == null)
                        {
                            _valve2ShortPulseTimer = Utils.DispatchOnce.Do((double)value / 1000, () =>
                            {
                                _valve2ShortPulseTimer = null;
                            });
                        }
                        break;
                }
            }
        }
    }
}
