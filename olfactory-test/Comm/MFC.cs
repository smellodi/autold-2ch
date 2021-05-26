using System;
using System.Runtime.InteropServices;
using System.IO.Ports;
using System.Threading;

namespace Olfactory.Comm
{
    public struct MFCChannel
    {
        public double Pressure;
        public double Temperature;
        public double VolumeFlow;
        public double MassFlow;
        public double Setpoint;
        public string Gas;

        public string ToString(char separator)
        {
            return string.Join(separator,
               new string[] {
                        MassFlow.ToString("F4"),
                        Pressure.ToString("F1"),
                        Temperature.ToString("F2"),
           });
        }

        public static string[] Header => new string[] {
            "M ml/m",
            "Pr PSIA",
            "Temp C",
        };
    }

    public struct MFCSample                                                 // Sample record/vector; all the measured values for one second
    {
        public long Time;                                                   // Sample time; milliseconds from start

        public MFCChannel A;                                                // Device A: fresh air tube
        public MFCChannel B;                                                // Device B: odor tube

        public string ToString(char separator = ' ')
        {
            return string.Join(separator,
                new string[] {
                        Time.ToString(),
                        A.ToString(separator),
                        B.ToString(separator),
            });
        }

        public static string[] Header
        {
            get
            {
                var list = new System.Collections.Generic.List<string>();
                list.AddRange(new string[] { "", "|", "A", "", "|", "B" });
                list.Add("\r\nTime");
                list.AddRange(MFCChannel.Header);       // A
                list.AddRange(MFCChannel.Header);       // B
                return list.ToArray();
            }
        }
    }

    public class MFC : CommPort<MFCSample>
    {
        public static MFC Instance => _instance = _instance ?? new();

        public enum Channel
        {
            A = 'a',
            B = 'b',
            Z = 'z',
        }

        /// <summary>
        /// These are actualy two bits to control two output valves bound to the MFC 'z' channel
        /// The lower bit controls the Valve #2: 0 - to waste, 1 - to user
        /// The higher bit controls the Valve #1: 0 - to waste, 1 - to system
        /// </summary>
        public enum OdorFlow
        { 
            ToWaste = 00,
            ToWasteAndUser = 01,        // probably, makes no sense
            ToSystemAndWaste = 10,
            ToSystemAndUser = 11,
        }

        public enum FlowEndPoint
        {
            User,
            Mixer
        }

        public override event EventHandler<Result> RequestResult = delegate { };
        public event EventHandler<string> Message = delegate { };

        public override string Name { get => "MFC"; }
        public override string[] DataColumns { get => MFCSample.Header; }

        public const double ODOR_MAX_SPEED = 128.0;
        public const double ODOR_MIN_SPEED = 1.0;


        // Control

        /// <summary>
        /// Fresh air flow speed, in liters per minute
        /// </summary>
        public double FreshAirSpeed
        {
            get => _freshAir;
            set
            {
                var result = SendCommand(FRESH_AIR_CHANNEL, CMD_SET, value.ToString("F1").Replace(',', '.'));
                if (result.Error == Error.Success)
                {
                    _freshAir = value;
                }
                RequestResult(this, result);
            }
        }

        /// <summary>
        /// Odor flow speed, in milliliters per minute
        /// </summary>
        public double OdorSpeed
        {
            get => _odor;
            set
            {
                var result = SendCommand(ODOR_CHANNEL, CMD_SET, value.ToString("F1").Replace(',', '.'));
                if (result.Error == Error.Success)
                {
                    _odor = value;
                }
                RequestResult(this, result);
            }
        }

        public OdorFlow OdorDirection
        {
            get => _odorDirection;
            set
            {
                var result = SendCommand(OUTPUT_CHANNEL, CMD_SET, ((int)value).ToString("D2"));
                if (result.Error == Error.Success)
                {
                    _odorDirection = value;
                }
                RequestResult(this, result);
            }
        }

        /// <summary>
        /// Private constructor, use Instance property to get the instance.
        /// Needs only to set the port stops bits
        /// </summary>
        private MFC() : base()
        {
            _portStopBits = StopBits.One;
        }

        /// <summary>
        /// Calculated the time in seconds for the odor to flow from the bottle to 
        /// 1 - the user
        /// 2 - the mixer
        /// given the current odor and fresh air speeds
        /// </summary>
        /// <param name="endPoint">the point for odor to reach</param>
        /// <param name="speed">Odor speed (the current one if omitted)</param>
        /// <returns>Time the odor reaches a user in seconds</returns>
        public double EstimateFlowDuration(FlowEndPoint endPoint, double speed = 0)
        {
            var odorTubeVolume = Math.PI * ODOR_TUBE_R * ODOR_TUBE_R * ODOR_TUBE_LENGTH / 1000;           // ml
            var odorSpeed = (speed <= 0 ? OdorSpeed : speed) / 60;      // ml/s

            var result = odorTubeVolume / odorSpeed;

            if (endPoint == FlowEndPoint.User)
            {
                var mixedTubeVolume = Math.PI * MIXED_TUBE_R * MIXED_TUBE_R * MIXED_TUBE_LENGTH / 1000;   // ml
                var mixedSpeed = 1000 * FreshAirSpeed / 60;             // ml/s

                result += mixedTubeVolume / mixedSpeed;
            }
            
            return result;
        }

        /// <summary>
        /// Calculates the speed in ml/min for the MFC-B (odor tube) that is required to 
        /// fill the tube between the bottle and the mixer with the odor
        /// </summary>
        /// <param name="time">The time to fill the tube in seconds</param>
        /// <returns>The speed in ml/min</returns>
        public double PredictFlowSpeed(double time)
        {
            var odorTubeVolume = Math.PI * ODOR_TUBE_R * ODOR_TUBE_R * ODOR_TUBE_LENGTH / 1000;       // ml
            return odorTubeVolume / time * 60;
        }

        /// <summary>
        /// Converts the ppm to the corresponding odor speed
        /// </summary>
        /// <param name="ppm">Odor concentration in ppm</param>
        /// <returns>Odor speed</returns>
        public double PPM2Speed(double ppm)
        {
            return 4.0 * ppm;   // TODO: implement this
        }

        /// <summary>
        /// Reads data into the buffer
        /// </summary>
        /// <param name="sample">Bufer to store data</param>
        /// <returns>Error code and description</returns>
        public override Result GetSample(out MFCSample sample)
        {
            Error error = Error.Success;
            string reason = "OK";

            sample = new MFCSample();

            if (!IsOpen)
            {
                return new Result()
                {
                    Error = Error.NotReady,
                    Reason = "The port is not open"
                };
            }

            lock (_mutex)
            {
                try
                {
                    if (error == Error.Success && _channels.HasFlag(Channels.A))
                    {
                        error = ReadChannelValues(Channel.A, out sample.A);
                    }

                    if (error == Error.Success && _channels.HasFlag(Channels.B))
                    {
                        error = ReadChannelValues(Channel.B, out sample.B);
                    }

                    if (error != Error.Success)
                    {
                        reason = "Issues in communicating the port";
                    }
                }
                catch (Exception ex)
                {
                    error = (Error)Marshal.GetLastWin32Error();
                    if (error == Error.Success) error = Error.ReadFault;        // just in case GetLastWin32Error returns 0
                    reason = "IO error: " + ex.Message;
                }

                if (error != Error.Success)
                {
                    Stop();
                }
                else
                {
                    sample.Time = Utils.Timestamp.Value;
                }

                _error = null;
            }

            return new Result() {
                Error = error,
                Reason = reason
            };
        }

        /// <summary>
        /// Before starting flor measureement, it is recommended to either set it to 0 for 2+ seconds,
        /// or to tare it down using this method
        /// </summary>
        /// <param name="channel">Channel ID</param>
        /// <returns></returns>
        public Result TareFlow(Channel channel)
        {
            return SendCommand(channel, CMD_TARE_FLOW);
        }

        /// <summary>
        /// Here, we check available channels (A and B).
        /// Note that there is channel Z that is maintaince output to user/waste,
        /// but we do not need to read it
        /// </summary>
        /// <returns>Error code and description</returns>
        protected override Result Initialize()
        {
            MFCSample sample = new MFCSample();
            Error error;

            _channels = Channels.None;

            try
            {
                if ((error = ReadChannelValues(Channel.A, out sample.A)) == Error.Success)
                {
                    _channels |= Channels.A;
                    _freshAir = sample.A.MassFlow;
                }
                if ((error = ReadChannelValues(Channel.B, out sample.B)) == Error.Success)
                {
                    _channels |= Channels.B;
                    _odor = sample.B.MassFlow;
                }
            }
            catch (Exception ex)
            {
                Stop();
                return new Result()
                {
                    Error = !IsDebugging ? (Error)Marshal.GetLastWin32Error() : Error.AccessFailed,
                    Reason = "IO error: " + ex.Message
                };
            }

            if (_channels == Channels.None)
            {
                Stop();
                return new Result()
                {
                    Error = error,
                    Reason = "Both MF controllers are unavailable"
                };
            }

            return new Result()
            {
                Error = Error.Success,
                Reason = "The port opened successfully"
            };
        }


        // Internal

        [Flags]
        enum Channels
        {
            None = 0,
            A = 1,
            B = 2,
            Both = A | B,
        }

        static MFC _instance;

        Channels _channels = Channels.None;

        double _freshAir = 5.0;
        double _odor = 4.0;
        OdorFlow _odorDirection = OdorFlow.ToSystemAndWaste;

        Mutex _mutex = new Mutex();     // this is needed to use in lock() only because we cannot use _port to lock when debugging

        const Channel FRESH_AIR_CHANNEL = Channel.A;
        const Channel ODOR_CHANNEL = Channel.B;
        const Channel OUTPUT_CHANNEL = Channel.Z;

        const string CMD_SET = "s";
        const string CMD_TARE_FLOW = "v";

        const char DATA_END = '\r';

        const double ODOR_TUBE_LENGTH = 500;     // mm
        const double ODOR_TUBE_R = 2;            // mm
        const double MIXED_TUBE_LENGTH = 1200;   // mm
        const double MIXED_TUBE_R = 2;           // mm

        /// <summary>
        /// Read the mass flow rate and the temperature of the specified MFC.
        /// The MFC responses are like:
        /// A +01006 +047.74 -0.00003 -00.002 +50.000    Air
        /// </summary>
        /// <param name="channel">MFC channel, either 'A' of 'B'</param>
        /// <param name="buffer">Data buffer</param>
        /// <returns>Error code</returns>
        Error ReadChannelValues(Channel channel, out MFCChannel buffer)
        {
            buffer = new MFCChannel();

            var mfcAddr = channel.ToString()[0];

            if (!IsDebugging)
            {
                char[] chars = new char[2] { mfcAddr, DATA_END };
                _port.Write(chars, 0, 2);
            }
            if (_error != null)
            {
                return (Error)Marshal.GetLastWin32Error();
            }

            var response = !IsDebugging ? ReadBytes() : EmulateReading(mfcAddr); // _port.ReadLine()
            if (_error != null)
            {
                return (Error)Marshal.GetLastWin32Error();
            }
            if (string.IsNullOrEmpty(response))
            {
                return Error.ReadFault;
            }

            string[] values = new string(response.Replace(',', '.')).Split(' ');
            if (values.Length < 7)
            {
                return Error.BadDataFormat;
            }

            try
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

                buffer.Pressure = double.Parse(values[1], culture);
                buffer.Temperature = double.Parse(values[2], culture);
                buffer.VolumeFlow = double.Parse(values[3], culture);
                buffer.MassFlow = double.Parse(values[4], culture);
                buffer.Setpoint = double.Parse(values[5], culture);
                buffer.Gas = values[6];
            }
            catch
            {
                return Error.InvalidData;
            }

            if (values[0][0] != mfcAddr)
            {
                return Error.WrongDevice;
            }

            return Error.Success;
        }

        /// <summary>
        /// Sends a specific command to the port
        /// </summary>
        /// <param name="channel">channel to send to</param>
        /// <param name="cmd">command to send</param>
        /// <param name="value">value to send</param>
        /// <returns>Error type and description</returns>
        Result SendCommand(Channel channel, string cmd, string value = "")
        {
            if (!IsOpen)
            {
                return new Result()
                {
                    Error = Error.NotReady,
                    Reason = "The port is not open"
                };
            }

            Error error;
            string reason;

            lock (_mutex)
            {
                string command = char.ToLower((char)channel) + cmd + value;
                var bytes = System.Text.Encoding.ASCII.GetBytes(command + DATA_END);
                if ((error = WriteBytes(bytes)) != Error.Success)
                {
                    Stop();
                    reason = $"Failed to send '{command}' to the port";
                }
                else
                {
                    reason = $"Command '{command}' sent successfully";
                }

                // we may have response to our request, lets read it after a short delay
                if (!IsDebugging)
                {
                    Thread.Sleep(50);

                    if (_port.BytesToRead > 0)  // note: it may appear that reading is need anyway, even if the buffer is empty yet, so thsi check si to be removed
                    {
                        string response = ReadBytes();
                        if (_error != null)
                        {
                            error = (Error)Marshal.GetLastWin32Error();
                        }
                    }
                }
            }

            return new Result()
            {
                Error = error,
                Reason = reason
            };
        }

        /// <summary>
        /// Reads bytes one by one until _portDataTerminator is met or time is out
        /// </summary>
        /// <returns>The string read from the port</returns>
        string ReadBytes()
        {
            string response = "";

            int duration = 0;
            int lastChar;

            try
            {
                while (duration < PORT_TIMEOUT)        // Wait for response; response ends in return
                {
                    lastChar = _port.ReadChar();
                    if (lastChar == 0)
                    {
                        Thread.Sleep(POLL_PERIOD);     // Can be actually twice as long, depending on timer precision.
                        duration += POLL_PERIOD;
                        continue;
                    }

                    if (lastChar == DATA_END)
                    {
                        break;
                    }
                    else
                    {
                        response += Convert.ToChar(lastChar);
                    }
                }
            }
            catch
            {
                response = "";
            }

            return response;
        }

        /// <summary>
        /// Writes bytes to the port
        /// </summary>
        /// <param name="bytes">Bytes to write</param>
        /// <returns>Error type</returns>
        Error WriteBytes(byte[] bytes)
        {
            Error result = Error.Success;

            try
            {
                if (!IsDebugging)
                {
                    _port.Write(bytes, 0, bytes.Length);
                    if (_error != null)
                    {
                        return (Error)Marshal.GetLastWin32Error();
                    }
                }
                else
                {
                    EmulateWriting(bytes);
                }
            }
            catch
            {
                result = Error.WriteFault;
            }

            return result;
        }


        // Debugging

        Random rnd = new Random((int)DateTime.Now.Ticks);
        int pressureA = 1200;
        int pressureB = 1800;
        double massFlowA = 1.0;
        double massFlowB = 0.02;
        double volFlowA = .05;
        double volFlowB = .05;
        double e(double amplitude) => (rnd.NextDouble() - 0.5) * 2 * amplitude;
        int e(int amplitude) => rnd.Next(-amplitude, amplitude);
        string EmulateReading(char channel)
        {
            if (rnd.NextDouble() < 0.002)
            {
                return "";
            }

            var pressure = channel == 'A' ? pressureA : pressureB;
            var massFlow = channel == 'A' ? massFlowA : massFlowB;
            var volFlow = channel == 'A' ? volFlowA : volFlowB;

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
        void EmulateWriting(byte[] command)
        {
            if (rnd.NextDouble() < 0.002)
            {
                throw new Exception("Simulating writing fault");
            }

            var cmd = System.Text.Encoding.Default.GetString(command);
            if (cmd.Length > 4)
            {
                Channel channel = (Channel)Enum.Parse(typeof(Channel), cmd[0].ToString(), true);
                string cmdID = cmd[1].ToString();
                if (cmdID == CMD_SET)
                {
                    double value = double.Parse(cmd.Substring(2, command.Length - 2));
                    switch (channel)
                    {
                        case Channel.A: massFlowA = value; break;
                        case Channel.B: massFlowB = value; break;
                        default: break;
                    }
                }    
            }
        }
    }
}
