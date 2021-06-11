using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Olfactory.Comm
{
    internal class PIDEmulator
    {
        public static PIDEmulator Instance => _instance ??= new();

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
                buffer[0] = PID.MODBUS_ADDR_PID;
                buffer[1] = PID.MODBUS_FN_PRESET_INPUT_REGS;
                buffer[2] = PID.MODBUS_REG_PID_POWER >> 8;
                buffer[3] = PID.MODBUS_REG_PID_POWER & 0xFF;
                buffer[4] = 0x00;
                buffer[5] = 0x01;
                buffer[6] = 0x84;
                buffer[7] = 0xA7;
            }
            else if (count == 29) // query-input
            {
                var addr = (inputQuery.AddressHi << 8) | inputQuery.AddressLo;

                buffer[0] = PID.MODBUS_ADDR_PID;
                buffer[1] = PID.MODBUS_FN_READ_INPUT_REGS;
                buffer[2] = PID.MODBUS_GROUP_LEN * sizeof(uint);

                if (addr == PID.MODBUS_REG_ADCMV_GROUP)
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
                    var pid = GetPID();
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
        PID.ModQueryPreset1Regs presetQuery;
        PID.ModQueryReadInputRegs inputQuery;

        MFCEmulator _mfc = MFCEmulator.Instance;

        
        
        // Flow model

        class Sample
        {
            public double Timestamp; // seconds
            public double Value;
            public Sample(double timestamp, double value)
            {
                Timestamp = timestamp;
                Value = value;
            }
        }

        DispatcherTimer _timer = new DispatcherTimer();
        Queue<Sample> _samples = new Queue<Sample>();

        const double SAMPLING_INTERVAL = 0.1;               // sec
        const double PID_DELAY = 1;                         // sec
        const double ODOR_TUBE_CAPACITY = 0.35;             // cc
        const double ODOR_TUBE_EVAPORATION_RATE = 0.05;     // /sec
        const double TUBE_SURFACE_CAPACITY = 2;             // /(cc/s)
        const double TUBE_SURFACE_EVAPORATION_RATE = 0.3;   // /sec
        const double TUBE_SURFACE_ACCUMULATION_RATE = 0.5;  // sec, to get the accumulation amount half from the max possible
        const double PID_AT_HALF_MEMBRANE_THROUGHPUT = 500; // mV

        // Emulates odor left in the tube after valve #1 and before gas mixer
        // Quickly reaches the maximum after the valve is opened, drops after the valve is closed
        double _odorInTube = 0;        // cc, for valve-closed state
        double _odorFlowed = 0;        // cc, for valve-opened state

        // Emulates odor stack to tube surface
        // Quickly reaches maximum after the valve is opened, drops after the valve is closed
        // The maximum depends on odor flow rate
        double _odorOnSurface = 0;     // cc

        // Memorizes when the valve was toggled
        bool _isValveOpened = false;

        double _currentPID = 28.2;

        private PIDEmulator()
        {
            System.Diagnostics.Debug.WriteLine("Time\tInTube\tOnSurf\tFlowed\tFlowing\tEvap\tPID\tValve");
            _timer.Interval = TimeSpan.FromSeconds(SAMPLING_INTERVAL);
            _timer.Tick += (s, e) =>
            {
                var isValveOpened = _mfc.OdorFlow >= MFC.OdorFlow.ToSystemAndWaste;
                if (isValveOpened != _isValveOpened)
                {
                    ToggleFlowState(isValveOpened);
                }

                var flowing = GetFlowingOdorAmount();
                var evaporating = GetEvaporatingOdorAmount(flowing);

                var pid = EstimatePID(flowing + evaporating);

                var ts = Utils.Timestamp.Value / 1000.0;
                _samples.Enqueue(new Sample(ts, pid));

                while (ts - _samples.Peek().Timestamp > PID_DELAY)
                {
                    _samples.Dequeue();
                }

                System.Diagnostics.Debug.WriteLine($"{ts:F2}\t{_odorInTube:F4}\t{_odorOnSurface:F4}\t{_odorFlowed:F4}\t{flowing:F4}\t{evaporating:F6}\t{GetPID().D}\t{(int)_mfc.OdorFlow / 10}");
            };
            _timer.Start();
        }

        private void ToggleFlowState(bool isValveOpened)
        {
            _isValveOpened = isValveOpened;

            if (_isValveOpened)
            {
                _odorFlowed = _odorInTube;
            }
            else
            {
                _odorInTube = Math.Min(ODOR_TUBE_CAPACITY, _odorFlowed);
                _odorFlowed = 0;
            }
        }

        private double GetFlowingOdorAmount()
        {
            double? result = null;
            var ofr = _mfc.OdorFlowRate / 60;   // cc/second

            if (_isValveOpened && ofr > 0)
            {
                var odorAmount = SAMPLING_INTERVAL * ofr;
                _odorFlowed += odorAmount;

                if (_odorFlowed > ODOR_TUBE_CAPACITY)
                {
                    result = odorAmount;
                }
            }


            if (result == null)
            {
                var odorAmountFromOdorTube = SAMPLING_INTERVAL * _odorInTube * ODOR_TUBE_EVAPORATION_RATE;
                _odorInTube = Math.Max(0, _odorInTube - odorAmountFromOdorTube);

                result = odorAmountFromOdorTube;
            }

            return result ?? 0;
        }

        private double GetEvaporatingOdorAmount(double flowing)
        {
            var odorOnSurfaceCapicity = flowing * TUBE_SURFACE_CAPACITY;  // assume this is linear, although it could be not
            var capacityDiff = odorOnSurfaceCapicity - _odorOnSurface;

            double amount;
            if (capacityDiff > 0)        // can stick more odor, accumulate it on surfaces
            {
                amount = flowing * TUBE_SURFACE_ACCUMULATION_RATE * (capacityDiff / (1 + capacityDiff));
            }
            else                         // too much, evaporate some
            {
                amount = SAMPLING_INTERVAL * capacityDiff * TUBE_SURFACE_EVAPORATION_RATE;
            }

            _odorOnSurface = Math.Max(0, _odorOnSurface + amount);

            return -amount;              // for evaporation, we reverse the sign of accumulated amount
        }

        private double EstimatePID(double odorAmountFlowingThroughPID)
        {
            var odorFlowRate = odorAmountFlowingThroughPID / SAMPLING_INTERVAL * 60;
            var expectedPID = -0.124 * odorFlowRate * odorFlowRate + 43.75 * odorFlowRate + 28.2;
            var pidMembraneThroughput = _currentPID / (PID_AT_HALF_MEMBRANE_THROUGHPUT + _currentPID);
            _currentPID += (expectedPID - _currentPID) * pidMembraneThroughput * SAMPLING_INTERVAL;

            return _currentPID;
        }

        private PID.BtoD GetPID()
        {
            var sample = _samples.Peek();

            PID.BtoD result = new PID.BtoD();
            result.D = (uint)(sample.Value + e(2.5));
            return result;
        }


        // emulates measurement inaccuracy
        double e(double amplitude) => (rnd.NextDouble() - 0.5) * 2 * amplitude;
        int e(int amplitude) => rnd.Next(-amplitude, amplitude);
    }
}
