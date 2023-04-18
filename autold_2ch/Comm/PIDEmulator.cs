using System;

namespace Olfactory2Ch.Comm
{
    public enum Gas
    {
        nButanol,
        IPA
    }

    internal class OlfactoryDeviceModel
    {
        /// <summary>
        /// Gas in the first channel
        /// </summary>
        public static Gas Gas1 = Gas.nButanol;

        /// <summary>
        /// Gas in the second channel
        /// </summary>
        public static Gas Gas2 = Gas.IPA;

        /// <summary>
        /// Current in mA
        /// </summary>
        public float Loop => 11.5f + (float)Math.Sin(Utils.Timestamp.Ms % 5000 * 0.072 * Math.PI / 180f); // 5s is the breathing cycle

        /// <summary>
        /// Current PID value in mV
        /// </summary>
        public double PID
        {
            get
            {
                double expected = PID_BASELINE +
                    (_mfcEmul.OdorDirection.HasFlag(MFC.ValvesOpened.Valve1) ? ToPID(Gas1, _mfcEmul.Odor1FlowRate) : 0) +
                    (_mfcEmul.OdorDirection.HasFlag(MFC.ValvesOpened.Valve2) ? ToPID(Gas2, _mfcEmul.Odor2FlowRate) : 0);

                double nextPID;
                if (_prevPID < expected)
                {
                    // in addition, we simulate some delay when the PID signal has to rise
                    double weightPrev = 75.0 / (0.0000000001 + Math.Pow(_prevPID - PID_BASELINE, 0.7)); // the numbers just guessed to get a delay and a curve somewhat realistic
                    double weightNext = 1.0 - WEIGHT;
                    nextPID = (_prevPID * weightPrev + expected * weightNext) / (weightPrev + weightNext);
                }
                else
                {
                    // ..and simpler simulation when the PID signal has to go down
                    nextPID = expected + (_prevPID - expected) * WEIGHT;
                }

                _prevPID = nextPID;
                return nextPID;
            }
        }

        /// <summary>
        /// Computes PID value when the specified gases are flowing at the specified rates
        /// </summary>
        /// <param name="gas1">Gas in the first channel</param>
        /// <param name="gas1Flow">First gas flow rate, ml/min</param>
        /// <param name="gas2">Gas in the second channel</param>
        /// <param name="gas2Flow">Second gas flow rate, ml/min</param>
        /// <returns>PID value</returns>
        public static double ComputePID(Gas gas1, double gas1Flow, Gas gas2, double gas2Flow)
        {
            double mv1 = ToPID(gas1, gas1Flow);
            double mv2 = ToPID(gas2, gas2Flow);
            return PID_BASELINE + mv1 + mv2;
        }

        // Internal

        const double WEIGHT = 0.85;
        const double PID_BASELINE = 50;

        readonly MFCEmulator _mfcEmul = MFCEmulator.Instance;

        double _prevPID = PID_BASELINE;

        /// <summary>
        /// Computes PID value for the given gas flowing at the specified rate
        /// </summary>
        /// <param name="gas">Gas type</param>
        /// <param name="flow">Flow rate, ml/min</param>
        /// <returns>PID value</returns>
        /// <exception cref="Exception"><see cref="Exception"/> if gas is unknown</exception>
        static double ToPID(Gas gas, double flow) => gas switch
        {
            Gas.nButanol => flow * 20,
            Gas.IPA => flow * 35,
            _ => throw new Exception($"Gas '{gas}' is unknown")
        };
    }

    internal class PIDEmulator
    {
        public static PIDEmulator Instance => _instance ??= new();

        public double PID => _model.PID;

        public void EmulateWrite<T>(T query)
        {
            if (query is PID.ModQueryPreset1Regs preset)
            {
                _presetQuery = preset;
            }
            else if (query is PID.ModQueryReadInputRegs input)
            {
                _inputQuery = input;
            }
        }

        public int EmulateReading(byte[] buffer, int offset, int count)
        {
            /*if (rnd.NextDouble() < 0.005)
            {
                throw new Exception("Simulating reading fault");
            }*/

            if (count == 8) // query-preset1
            {
                buffer[0] = Comm.PID.MODBUS_ADDR_PID;
                buffer[1] = Comm.PID.MODBUS_FN_PRESET_INPUT_REGS;
                buffer[2] = Comm.PID.MODBUS_REG_PID_POWER >> 8;
                buffer[3] = Comm.PID.MODBUS_REG_PID_POWER & 0xFF;
                buffer[4] = 0x00;
                buffer[5] = 0x01;
                buffer[6] = 0x84;
                buffer[7] = 0xA7;
            }
            else if (count == 29) // query-input
            {
                var addr = (_inputQuery.AddressHi << 8) | _inputQuery.AddressLo;

                buffer[0] = Comm.PID.MODBUS_ADDR_PID;
                buffer[1] = Comm.PID.MODBUS_FN_READ_INPUT_REGS;
                buffer[2] = Comm.PID.MODBUS_GROUP_LEN * sizeof(uint);

                if (addr == Comm.PID.MODBUS_REG_ADCMV_GROUP)
                {
                    // rtd
                    buffer[3] = (byte)'T';
                    buffer[4] = (byte)'R';
                    buffer[5] = 0;
                    buffer[6] = (byte)'D';
                    // ref = 10000
                    buffer[7] = 0x27;
                    buffer[8] = 0x10;
                    buffer[9] = 0;
                    buffer[10] = 0;
                    // pid
                    var pid = new PID.BtoD() { D = (uint)(_model.PID + e(2.5)) };
                    buffer[11] = pid.B1;
                    buffer[12] = pid.B0;
                    buffer[13] = pid.B3;
                    buffer[14] = pid.B2;
                    // light
                    buffer[15] = (byte)'G';
                    buffer[16] = (byte)'L';
                    buffer[17] = (byte)'T';
                    buffer[18] = (byte)'H';
                    // temp
                    buffer[19] = (byte)'E';
                    buffer[20] = (byte)'T';
                    buffer[21] = (byte)'P';
                    buffer[22] = (byte)'M';
                    // currloop
                    buffer[23] = (byte)'R';
                    buffer[24] = (byte)'C';
                    buffer[25] = (byte)'P';
                    buffer[26] = (byte)'L';
                }
                else if (addr == Comm.PID.MODBUS_REG_SIGNAL_GROUP)
                {
                    // rtd
                    var rtd = new PID.BtoD
                    {
                        f = 25f + (float)e(0.15)
                    };
                    buffer[3] = rtd.B1;
                    buffer[4] = rtd.B0;
                    buffer[5] = rtd.B3;
                    buffer[6] = rtd.B2;
                    // ref
                    var ref10V = new PID.BtoD
                    {
                        f = 20f
                    };
                    buffer[7] = ref10V.B1;
                    buffer[8] = ref10V.B0;
                    buffer[9] = ref10V.B3;
                    buffer[10] = ref10V.B2;
                    // pid
                    var pid = new PID.BtoD
                    {
                        f = 45f + (float)e(1.5)
                    };
                    buffer[11] = pid.B1;
                    buffer[12] = pid.B0;
                    buffer[13] = pid.B3;
                    buffer[14] = pid.B2;
                    // light
                    var light = new PID.BtoD
                    {
                        f = 1f
                    };
                    buffer[15] = light.B1;
                    buffer[16] = light.B0;
                    buffer[17] = light.B3;
                    buffer[18] = light.B2;
                    // temp
                    var temp = new PID.BtoD
                    {
                        f = 25f + (float)e(0.05)
                    };
                    buffer[19] = temp.B1;
                    buffer[20] = temp.B0;
                    buffer[21] = temp.B3;
                    buffer[22] = temp.B2;
                    // currloop
                    var cl = new PID.BtoD
                    {
                        f = _model.Loop + (float)e(0.05)
                    };
                    buffer[23] = cl.B1;
                    buffer[24] = cl.B0;
                    buffer[25] = cl.B3;
                    buffer[26] = cl.B2;
                }
                // crc
                var crc = new PID.BtoW
                {
                    W = Comm.PID.CRC16(buffer, count - sizeof(ushort))
                };
                buffer[offset + count - 2] = crc.B1;
                buffer[offset + count - 1] = crc.B0;
            }
            else
            {
                throw new Exception("Simulator fault");
            }

            return count;
        }

        // Internal

        static PIDEmulator _instance;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);
        readonly OlfactoryDeviceModel _model = new();

        PID.ModQueryPreset1Regs _presetQuery;
        PID.ModQueryReadInputRegs _inputQuery;

        private PIDEmulator() { }

        // emulates measurement inaccuracy
        double e(double amplitude) => (_rnd.NextDouble() - 0.5) * 2 * amplitude;
        int e(int amplitude) => _rnd.Next(-amplitude, amplitude);
    }
}
