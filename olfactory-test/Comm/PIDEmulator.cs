using System;

namespace Olfactory.Comm
{
    internal class PIDEmulator
    {
        public static PIDEmulator Instance => _instance ??= new();

        public OlfactoryDeviceModel Model => _model;

        public void EmulateWrite<T>(T query)
        {
            if (query is PID.ModQueryPreset1Regs preset)
            {
                presetQuery = preset;
            }
            else if (query is PID.ModQueryReadInputRegs input)
            {
                inputQuery = input;
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
                var addr = (inputQuery.AddressHi << 8) | inputQuery.AddressLo;

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
                    PID.BtoD pid = new PID.BtoD() { D = (uint)(_model.PID + e(2.5)) };
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
                else if (addr == PID.MODBUS_REG_SIGNAL_GROUP)
                {
                    // rtd
                    var rtd = new PID.BtoD();
                    rtd.f = 25f + (float)e(0.15);
                    buffer[3] = rtd.B1;
                    buffer[4] = rtd.B0;
                    buffer[5] = rtd.B3;
                    buffer[6] = rtd.B2;
                    // ref
                    var ref10V = new PID.BtoD();
                    ref10V.f = 20f;
                    buffer[7] = ref10V.B1;
                    buffer[8] = ref10V.B0;
                    buffer[9] = ref10V.B3;
                    buffer[10] = ref10V.B2;
                    // pid
                    var pid = new PID.BtoD();
                    pid.f = 45f + (float)e(1.5);
                    buffer[11] = pid.B1;
                    buffer[12] = pid.B0;
                    buffer[13] = pid.B3;
                    buffer[14] = pid.B2;
                    // light
                    var light = new PID.BtoD();
                    light.f = 1f;
                    buffer[15] = light.B1;
                    buffer[16] = light.B0;
                    buffer[17] = light.B3;
                    buffer[18] = light.B2;
                    // temp
                    var temp = new PID.BtoD();
                    temp.f = 29f;
                    buffer[19] = temp.B1;
                    buffer[20] = temp.B0;
                    buffer[21] = temp.B3;
                    buffer[22] = temp.B2;
                    // currloop
                    var cl = new PID.BtoD();
                    cl.f = 5.678f + (float)e(0.15);
                    buffer[23] = cl.B1;
                    buffer[24] = cl.B0;
                    buffer[25] = cl.B3;
                    buffer[26] = cl.B2;
                }
                // crc
                var crc = new PID.BtoW();
                crc.W = PID.CRC16(buffer, count - sizeof(ushort));
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

        Random rnd = new Random((int)DateTime.Now.Ticks);
        OlfactoryDeviceModel _model = new OlfactoryDeviceModel();

        PID.ModQueryPreset1Regs presetQuery;
        PID.ModQueryReadInputRegs inputQuery;

        private PIDEmulator() { }

        // emulates measurement inaccuracy
        double e(double amplitude) => (rnd.NextDouble() - 0.5) * 2 * amplitude;
        int e(int amplitude) => rnd.Next(-amplitude, amplitude);
    }
}
