using System;
using System.Collections.Generic;
using System.Windows;

namespace Olfactory.Comm
{
    public class OlfactoryDeviceModel
    {
        public double PID => _samples.Count > 0 ? _samples.Peek().Value : 0;

        public OlfactoryDeviceModel()
        {
            _timer.Interval = EmulatorTimestamp.SAMPLING_INTERVAL * 1000;
            _timer.Elapsed += (s, e) => UpdatePID(EmulatorTimestamp.Value);
            _timer.Start();
        }


        /// <summary>
        /// For debugging purposes: generates a pulse of odor and saves the PID value estimation variables to clipboard
        /// </summary>
        /// <param name="id">Odor level, 0..9 => 5-80 ml/min </param>
        public void PulseInput(int id)
        {
            double[] ODOR_FLOW_RATES = new double[] { 5, 10, 15, 20, 25, 30, 35, 40, 50, 80 };

            if (!MFC.Instance.IsOpen)
            {
                System.Media.SystemSounds.Hand.Play();
                return;
            }

            _timer.Stop(); // pause normal processing

            _odorInTube = 0;
            _odorFlowed = 0;
            _odorOnSurface = 0;
            _odorInPID = 0;
            _odorInPIDInnerTube = 0;
            _samples.Clear();

            List<string> log = new List<string>();
            log.Add("Time\tInput\tAccum\tTube\tPID In\tPID Od\tPID mV");

            double ts = EmulatorTimestamp.Start();

            MFC.Instance.OdorSpeed = ODOR_FLOW_RATES[id];

            while (ts < 56)
            {
                var (pidInput, pidOdor) = UpdatePID(ts);

                var input = _mfc.OdorFlowRate * ((int)_mfc.OdorDirection / 10);
                var logRecord = $"{ts:F2}\t{input:F4}\t{_odorFlowed:F4}\t{_odorInTube:F4}\t{pidInput:F4}\t{pidOdor:F6}\t{PID}";

                log.Add(logRecord);

                var newTs = EmulatorTimestamp.Next;

                if (ts < 5 && 5 < newTs)
                {
                    MFC.Instance.OdorDirection = MFC.OdorFlowsTo.SystemAndUser;
                }
                else if (ts < 37 && 37 < newTs)
                {
                    MFC.Instance.OdorDirection = MFC.OdorFlowsTo.Waste;
                }

                ts = newTs;
            }

            EmulatorTimestamp.Stop();

            Clipboard.SetText(string.Join('\n', log));

            System.Media.SystemSounds.Asterisk.Play();

            _timer.Start();  // resume normal processing
        }

        /// <summary>
        /// For debugging purposes: imitates a feedback loop
        /// The app controls the MFC-B until the last 3 samples of PID value stay within a given range
        /// </summary>
        /// <param name="id">PID value, 0..9 => 200-2700 mV </param>
        public void PulseOutput(int id)
        {
            //double[] PID_OUTPUTS = new double[] { 200, 300, 600, 900, 1200, 1500, 1800, 2100, 2400, 2700 };
            double[] ODOR_FLOW_RATES = new double[] { 5, 10, 15, 20, 25, 30, 35, 40, 50, 80 };

            if (!MFC.Instance.IsOpen)
            {
                System.Media.SystemSounds.Hand.Play();
                return;
            }

            _timer.Stop(); // pause normal processing

            _odorInTube = 0;
            _odorFlowed = 0;
            _odorOnSurface = 0;
            _odorInPID = 0;
            _odorInPIDInnerTube = 0;
            _samples.Clear();

            _flStableLevelSampleCount = 0;
            _isFlStableLevelReached = false;

            List<string> log = new List<string>();
            log.Add("Time\tTarget PID\tInput\tError\tDBG\tDBG2\tPID mV");

            double ts = EmulatorTimestamp.Start();

            var targetOdor = ODOR_FLOW_RATES[id];
            var targetPID = GetPIDValue(targetOdor);

            // Initially, we set the odor flow to the max posible rate
            MFC.Instance.OdorSpeed = FL_MAX_ODOR_FLOW_RATE;
            MFC.Instance.OdorDirection = MFC.OdorFlowsTo.SystemAndUser;

            // We then set the neasest odor flow rate update after the valve-mixer tube is full
            var nextMFCUpdateTs = 0.4; // can we comute this aumatically based on tube capacity and FL_MAX_ODOR_FLOW_RATE (minus STEP)?
            
            // Emulate 60 seconds, with valve closed after 30 seconds
            while (ts < 60)
            {
                UpdatePID(ts);

                if (ts > nextMFCUpdateTs) // every half a second.. 
                {
                    nextMFCUpdateTs += 0.3;
                    MFC.Instance.OdorSpeed = UpdateMFC(targetPID, targetOdor);
                }

                var input = _mfc.OdorFlowRate * ((int)_mfc.OdorDirection / 10);
                var logRecord = $"{ts:F2}\t{targetPID:F4}\t{input:F4}\t{targetPID-PID:F4}\t{_dbg1:F4}\t{_dbg2}\t{PID}";
                log.Add(logRecord);
                
                var newTs = EmulatorTimestamp.Next;

                if (ts < 30 && 30 < newTs)
                {
                    MFC.Instance.OdorDirection = MFC.OdorFlowsTo.Waste;
                    targetPID = PID_P2_C;
                    targetOdor = 0;
                }

                ts = newTs;
            }

            EmulatorTimestamp.Stop();

            Clipboard.SetText(string.Join('\n', log));

            System.Media.SystemSounds.Asterisk.Play();

            _timer.Start();  // resume normal processing
        }


        // Internal

        // -------------------
        // PID emulation model
        // -------------------

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


        MFCEmulator _mfc = MFCEmulator.Instance;
        System.Timers.Timer _timer = new System.Timers.Timer();
        Queue<Sample> _samples = new Queue<Sample>();

        // Model parameters for clean air 5 l/min:

        // General delay of the measurements (most likely, because its takes time for odor molecules
        // to get through the membrane)
        const double PID_DELAY = 0.05;                    // sec

        // NOTE: the next value(s) are measured and modelled from tests.
        // Valve1-GasMixer tube capacity
        const double ODOR_TUBE_CAPACITY = 0.35;        // ml

        // Controls the rate of odor leaking from the Valve1-GasMixer tube
        const double ODOR_TUBE_LEAK_RATE = 0.03;            // /sec

        // Control the gas compression speed in Valve1-GasMixer tube tube:
        // the larger the value, the slower the compression occurs
        // and the odor delay increases
        const double GAS_COMPRESSION_K = 2.5;

        // GasMixer-PID tube surface capacity
        const double TUBE_SURFACE_CAPACITY = 0;       // ml

        // Controls accumulation/evaporation rate of odor on/from GasMixer-PID tube surface
        // The higher this value, the slower the accumulation/evaporation proceeds
        const double TUBE_SURFACE_AV_K = 1;

        // Controls the amount of odor in PID extra volume/tube:
        // - value #1: the higher it is, the more odor is available to flow to PID after the valve is closed
        // - value #2: the lower it is, the less non-linearity affects low odor concentrations
        const double PID_EXTRA_VOLUME_RATIO_1 = 0.6;
        const double PID_EXTRA_VOLUME_RATIO_2 = 7;

        // Controls the speed of odor leaking from the PID extra volume/tube:
        // the high this value, the faster the odor is consumed after the valve is closed
        const double PID_EXTRA_VOLUME_FLOW_RATE = 0.15;

        // Limits odor removal from PID directly to the PID extra volume/tube when the valve is opened
        const double PID_MAX_INNER_ABSORBTION = 0.0001;

        // Starting level of membrane throughput 
        const double PID_MEMBRANE_BASE_THROUGHPUT = 0.4;

        // Controls the ability of membrane to pass odor depending on the gradient of odor inside and outside PID
        const double PID_MEMBRANE_THROUGHPUT_RATE = 3;

        // Threhsold for conversion:
        //  - use 2nd-order polynomial approximation below this value,
        //  - use linear approximation above this value
        // The approximations are mixed using a sigmoid
        // The value can be compted by solving the equation d(PID_P2_A*x*x + PID_P2_B*x + PID_P2_C)/dx = d(PID_LN_A*x + PID_LN_B)/dx
        //      i.e. x = (PID_LN_A - PID_P2_B) / (-2 * PID_P2_A)
        const double PID_CONVERSION_THRESHOLOD = 52.5;   // ml/min

        // NOTE: the next value(s) are measured and modelled from tests.
        // 2nd order polynom coefficients to convert odor in PID (ml) to PID output (mV)

        // Undiluted N-Butanol:

        // Second-order polynom
        const double PID_P2_A = -0.108;
        const double PID_P2_B = 42.57;
        const double PID_P2_C = 53.1;  // baseline

        // Linear
        const double PID_LN_A = 31.23;
        const double PID_LN_B = 340.27;



        // Model state:

        // Emulates odor left in the tube valve-to-mixer
        // The time it reaches the maximum after the valve is opened depends on the MFC rate.
        // It drops with a constant rate after the valve is closed
        double _odorInTube = 0;        // ml, for valve-closed state
        double _odorFlowed = 0;        // ml, for valve-opened state

        // Emulates odor stack to tube surface
        // Quickly reaches maximum after the valve is opened, drops after the valve is closed
        // The maximum value depends on odor flow rate
        double _odorOnSurface = 0;     // ml

        // Emulates odor amount in PID beyond the membrane where odor molecules are detected
        double _odorInPID = 0;

        // Emulates additional volume/tube inside PID
        // Odor in this volume/tube is absorbed mostly directly from the outer tube,
        // then it flows into main PID volume and does not evaporate back to the outer tube
        double _odorInPIDInnerTube = 0;

        // Memorizes the valve state to flush the tubes after it is closed
        bool _isValveOpened = false;


        /// <summary>
        /// Estimates the PID output value
        /// </summary>
        /// <param name="timestamp">Current timestamp</param>
        /// <returns>PID input and PID odor</returns>
        private (double, double) UpdatePID(double timestamp)
        {
            var isValveOpened = _mfc.OdorDirection >= MFC.OdorFlowsTo.SystemAndWaste;
            if (isValveOpened != _isValveOpened)
            {
                ToggleFlowState(isValveOpened);
            }

            var gasMixerInput = GetInputToGasMixer();
            var pidInput = GetInputToPID(gasMixerInput);

            var pidOdor = GetOdorInPID(pidInput);

            var pidOdorFlowRate = pidOdor / EmulatorTimestamp.SAMPLING_INTERVAL * 60;  // convert to ml/min
            var pidValue = GetPIDValue(pidOdorFlowRate);

            _samples.Enqueue(new Sample(timestamp, pidValue));

            while (timestamp - _samples.Peek().Timestamp > PID_DELAY)
            {
                _samples.Dequeue();
            }

            return (pidInput, pidOdor);
        }

        /// <summary>
        /// Changes the system state when the valve is toggled
        /// </summary>
        /// <param name="isValveOpened">State of Valve #1</param>
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

        /// <summary>
        /// Models odor amount entering the gas mixer from the tube connected to Valve #1.
        /// The output of this tube has no odor yet right after the value is opened and MFC-B is set to >0,
        /// as it takes some time to flow the odored gas through the tube.
        /// After the valve is closed and/or MFC-B is set to 0, the odor leaks from this tube.
        /// </summary>
        /// <returns>Amount of odor flowing through PID from Valve2Mixer tube (ml ~ ppm)</returns>
        private double GetInputToGasMixer()
        {
            double? result = null;
            var ofr = _mfc.OdorFlowRate / 60;   // ml/sec

            if (_isValveOpened && ofr > 0)
            {
                var odorAmount = EmulatorTimestamp.SAMPLING_INTERVAL * ofr;
                _odorFlowed += odorAmount;

                var d = _odorFlowed - ODOR_TUBE_CAPACITY;
                if (d > 0)
                {
                    // compression of gas
                    var copressionDelay = 1.0 / (GAS_COMPRESSION_K + d);
                    var r = d * d / (copressionDelay + d * d);

                    result = odorAmount * r;
                }
            }


            if (result == null)
            {
                var odorLeak = EmulatorTimestamp.SAMPLING_INTERVAL * _odorInTube * ODOR_TUBE_LEAK_RATE;
                _odorInTube = Math.Max(0, _odorInTube - odorLeak);

                result = odorLeak;
            }

            return result ?? 0;
        }

        /// <summary>
        /// Models odor accumulation on / evaporation from surface of the tube that connects gas mixer and PID
        /// The output may be lower than the input if odor is accumulating, and greater if odor is evaporating
        /// </summary>
        /// <param name="gasMixerOutput">Odor input, ml</param>
        /// <returns>Odor output, ml</returns>
        private double GetInputToPID(double gasMixerOutput)
        {
            var odorOnSurfaceCapicity = gasMixerOutput * TUBE_SURFACE_CAPACITY;  // assume this is linear, although it could be not
            var diff = odorOnSurfaceCapicity - _odorOnSurface;
            var x = Math.Abs(diff);

            double amount = diff * (x / (TUBE_SURFACE_AV_K + x));

            _odorOnSurface = Math.Max(0, _odorOnSurface + amount);

            return gasMixerOutput - amount;
        }

        /// <summary>
        /// Models the amount of odor inside PID, both in the space where the measurement occurs
        /// and in the extra volume/tube
        /// </summary>
        /// <param name="input">Odor amount in proximity to the PID membrane (ml)</param>
        /// <returns>Odor in PID, ml</returns>
        private double GetOdorInPID(double input)
        {
            // Extra volume in PID where odor may accumulate
            var odorInExtraVolume = _odorInPID * PID_EXTRA_VOLUME_RATIO_1 * (1 + _odorInPID) / (1 + PID_EXTRA_VOLUME_RATIO_2 * _odorInPID);
            var innerDiff = odorInExtraVolume - _odorInPIDInnerTube;
            var innerFlow = innerDiff * PID_EXTRA_VOLUME_FLOW_RATE;
            _odorInPIDInnerTube += innerFlow * EmulatorTimestamp.SAMPLING_INTERVAL;

            innerFlow = Math.Min(PID_MAX_INNER_ABSORBTION, innerFlow); // odor comes into the inner tube mostly from outside, not from PID

            // Odor absorbtion / evaporation through membrane
            var outerDiff = input - _odorInPID;
            var throughput = PID_MEMBRANE_BASE_THROUGHPUT + Math.Abs(outerDiff) * PID_MEMBRANE_THROUGHPUT_RATE;
            var outerFlow = outerDiff * throughput;
            _odorInPID += (outerFlow - innerFlow) * EmulatorTimestamp.SAMPLING_INTERVAL;

            return _odorInPID;
        }

        /// <summary>
        /// Converts odor in PID to mV
        /// </summary>
        /// <param name="input">Odor amount, ml/min</param>
        /// <returns>PID value, mV</returns>
        private double GetPIDValue(double input)
        {
            var polynomial = PID_P2_A * input * input + PID_P2_B * input + PID_P2_C;
            var linear = PID_LN_A * input + PID_LN_B;

            var weight = input - PID_CONVERSION_THRESHOLOD;
            weight /= Math.Sqrt(1 + weight * weight);
            weight = (weight + 1) / 2;

            return polynomial * (1.0 - weight) + linear * weight;
        }


        // -------------------
        // Feedback loop
        // -------------------
        
        const double FL_MAX_ODOR_FLOW_RATE = 90;
        const double FL_ACCURACY = 0.005;       // 0.5%
        const double FL_GAIN = 22;
        const double FL_GAIN_THRESHOLD = 15;    // ml/min
        const double FL_STABLE_LEVEL_DURATION_THRESHOLD = 8;

        double _dbg1 = 0;
        double _dbg2 = 0;
        int _flStableLevelSampleCount = 0;
        bool _isFlStableLevelReached = false;

        private double UpdateMFC(double targetPID, double targetOdor)
        {
            var result = targetOdor;

            var pid = PID;

            var targetPidDistance = targetPID - pid;
            var targetPidDistanceAbs = Math.Abs(targetPidDistance);
            if (!_isFlStableLevelReached && targetPidDistanceAbs > FL_ACCURACY * targetPID)
            {
                _flStableLevelSampleCount = 0;

                // This model is rather presice if there is no delay in PID measurements (PID_DELAY < 0.1 sec)
                var gain = FL_GAIN * (1.0 - 0.6 * targetOdor / Math.Sqrt(FL_GAIN_THRESHOLD * FL_GAIN_THRESHOLD + targetOdor * targetOdor));
                var targetPidDistanceRel = targetPidDistance / targetPID;
                var newOdorFlow = targetOdor * (1.0 + targetPidDistanceRel * gain);

                _dbg1 = gain;
                result = Math.Max(1.0, Math.Min(FL_MAX_ODOR_FLOW_RATE, newOdorFlow));
            }
            else
            {
                _flStableLevelSampleCount++;
                _isFlStableLevelReached = _flStableLevelSampleCount >= FL_STABLE_LEVEL_DURATION_THRESHOLD;
            }

            _dbg2 = _flStableLevelSampleCount;
            return result;
        }
    }
}
