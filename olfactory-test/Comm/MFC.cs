using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Olfactory.Comm
{
    public struct MFCChannel
    {
        public double Pressure { get; set; }
        public double Temperature { get; set; }
        public double VolumeFlow { get; set; }
        public double MassFlow { get; set; }
        public double Setpoint { get; set; }
        public string Gas { get; set; }

        public string ToString(char separator) => string.Join(separator, new string[] {
            MassFlow.ToString("F4"),
            Pressure.ToString("F1"),
            Temperature.ToString("F2"),
        });

        public static string[] Header => new string[] {
            "M ml/m",
            "Pr PSIA",
            "Temp C",
        };
    }

    public struct MFCSample : ISample               // Sample record/vector; all the measured values for one second
    {
        /// <summary>
        /// Sample time; milliseconds from start
        /// </summary>
        public long Time { get; set; }

        /// <summary>
        /// Fresh air channel
        /// </summary>
        public MFCChannel A { get; set; }

        /// <summary>
        /// Odored air channel
        /// </summary>
        public MFCChannel B { get; set; }

        public double MainValue => B.MassFlow;

        public override string ToString() => string.Join('\t', new string[] {
            Time.ToString(),
            A.ToString('\t'),
            B.ToString('\t'),
        });

        public static string[] Header
        {
            get
            {
                var list = new List<string>();
                list.AddRange(new string[] { "", "|", "A", "", "|", "B", "", "|\r\nTime" });
                list.AddRange(MFCChannel.Header);       // A
                list.AddRange(MFCChannel.Header);       // B
                return list.ToArray();
            }
        }
    }

    /// <summary>
    /// Argument to be used in events fired inresponse to a command send to a device
    /// </summary>
    public class CommandResultArgs : EventArgs
    {
        public readonly string Command;
        public readonly string Value;
        public readonly Result Result;
        public CommandResultArgs(string command, string value, Result result)
        {
            Command = command;
            Value = value;
            Result = result;
        }
    }


    public class MFC : CommPort<MFCSample>
    {
        public static MFC Instance => _instance ??= new();

        public enum Channel
        {
            A = 'a',
            B = 'b',
            Z = 'z',
        }

        public enum Register
        {
            PULL_IN_0 = 1,
            PULL_IN_1 = 2,
            HOLD_0 = 3,
            HOLD_1 = 4
        }

        /// <summary>
        /// These are actualy two bits to control two output valves bound to the MFC 'z' channel
        /// The lower bit controls the Valve #2: 0 - to waste, 1 - to user
        /// The higher bit controls the Valve #1: 0 - to waste, 1 - to system
        /// </summary>
        [Flags]
        public enum OdorFlowsTo
        { 
            Waste = 0,
            User = 1,
            System = 10,
            WasteAndUser = Waste | User,        // makes no sense
            SystemAndWaste = System | Waste,
            SystemAndUser = System | User,
        }

        public enum FlowStartPoint
        {
            Chamber,
            Valve1
        }

        public enum FlowEndPoint
        {
            User,
            Mixer
        }

        public event EventHandler<CommandResultArgs> CommandResult;
        public event EventHandler<string> Message = delegate { };

        public override string Name => "MFC";
        public override string[] DataColumns => MFCSample.Header;

        public const double ODOR_MAX_SPEED = 90.0;
        public const double ODOR_MIN_SPEED = 0.0;


        // Control

        /// <summary>
        /// Fresh air flow speed, in liters per minute
        /// </summary>
        public double FreshAirSpeed
        {
            get => _freshAir;
            set
            {
                var val = value.ToString("F1").Replace(',', '.');
                var result = SendCommand(FRESH_AIR_CHANNEL, CMD_SET, val);
                if (result.Error == Error.Success)
                {
                    _freshAir = value;
                }
                CommandResult?.Invoke(this, new CommandResultArgs(FRESH_AIR_CHANNEL + CMD_SET, val, result));
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
                var val = value.ToString("F1").Replace(',', '.');
                var result = SendCommand(ODOR_CHANNEL, CMD_SET, val);
                if (result.Error == Error.Success)
                {
                    _odor = value;
                }
                CommandResult(this, new CommandResultArgs(ODOR_CHANNEL + CMD_SET, val, result));
            }
        }

        public OdorFlowsTo OdorDirection
        {
            get => _odorDirection;
            set
            {
                var val = ((int)value).ToString("D2");
                var result = SendCommand(OUTPUT_CHANNEL, CMD_SET, val);
                if (result.Error == Error.Success)
                {
                    _odorDirection = value;
                }
                CommandResult?.Invoke(this, new CommandResultArgs(OUTPUT_CHANNEL + CMD_SET, val, result));
            }
        }

        /// <summary>
        /// The flag that can be set on when creating flow pulses using valve controller own timer. 
        /// Only the output valve is allowed to operate in short-pulse mode, and gas-mixer valve always operates in "normal" mode.
        /// A pulse must be shorter than <see cref="MAX_SHORT_PULSE_DURATION"/> seconds.
        /// Note that even though the valve is closed automatically, the <see cref="OdorDirection"/> must be set manually
        /// when a pulse ends.
        /// </summary>
        public bool IsInShortPulseMode
        {
            get => _isInShortPulseMode;
            set
            {
                if (value && !_isInShortPulseMode)
                {
                    // only the output valve can operate in short-pulse mode
                    _isInShortPulseMode = SetRegisters((Register.HOLD_1, 0)).Error == Error.Success;
                }
                else if (!value && _isInShortPulseMode)
                {
                    _isInShortPulseMode = !(SetRegisters((Register.HOLD_1, 255), (Register.PULL_IN_1, 0)).Error == Error.Success);
                }
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

        public override void Stop()
        {
            if (IsOpen)
            {
                OdorSpeed = ODOR_MIN_SPEED;
            }

            base.Stop();
        }

        /// <summary>
        /// Calculated the time in seconds for the odor to flow from 
        /// 1 - the bottle
        /// 2 - the valve #1
        /// to 
        /// 1 - the user
        /// 2 - the mixer
        /// given the current odor and fresh air speeds
        /// </summary>
        /// <param name="startPoint">the starting point</param>
        /// <param name="endPoint">the point for odor to reach</param>
        /// <param name="speed">Odor speed (the current one if omitted)</param>
        /// <returns>Time the odor reaches a user in seconds</returns>
        public double EstimateFlowDuration(FlowStartPoint startPoint, FlowEndPoint endPoint, double speed = 0)
        {
            double result = 0;

            var odorSpeed = (speed <= 0 ? OdorSpeed : speed) / 60;      // ml/s

            if (startPoint == FlowStartPoint.Chamber)
            {
                var odorTubeVolume = Math.PI * TUBE_R * TUBE_R * ODOR_TUBE_LENGTH / 1000;           // ml

                result += odorTubeVolume / odorSpeed;
            }

            var vmTubeVolume = Math.PI * TUBE_R * TUBE_R * VALVE_MIXER_TUBE_LENGTH / 1000;           // ml
            result += vmTubeVolume / odorSpeed;

            if (endPoint == FlowEndPoint.User)
            {
                var mixedTubeVolume = Math.PI * TUBE_R * TUBE_R * MIXED_TUBE_LENGTH / 1000;   // ml
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
            var odorTubeVolume = Math.PI * TUBE_R * TUBE_R * ODOR_TUBE_LENGTH / 1000;       // ml
            return odorTubeVolume / time * 60;
        }

        /// <summary>
        /// Converts the ppm to the corresponding odor speed
        /// </summary>
        /// <param name="ppm">Odor concentration in ppm</param>
        /// <returns>Odor speed</returns>
        public double PPM2Speed(double ppm)
        {
            return 1.0 * ppm;   // TODO: implement this
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
                        error = ReadChannelValues(Channel.A, out MFCChannel freshAir);
                        sample.A = freshAir;
                    }

                    if (error == Error.Success && _channels.HasFlag(Channels.B))
                    {
                        error = ReadChannelValues(Channel.B, out MFCChannel odor);
                        sample.B = odor;
                    }

                    if (error != Error.Success)
                    {
                        reason = "Issues in communicating the port";
                    }
                }
                catch (Exception ex)
                {
                    error = (Error)ex.HResult;
                    reason = "IO error: " + ex.Message;
                }

                if (error != Error.Success)
                {
                    Stop();
                }
                else
                {
                    sample.Time = Utils.Timestamp.Ms;
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
        /// Prepares the output valve for a short flow pulse to the user using the valves controller timer.
        /// Note that gas-mixer valve is not affected.
        /// A pulse must be shorter than <see cref="MAX_SHORT_PULSE_DURATION"/> seconds.
        /// Note that even though the valve is closed automatically, the <see cref="OdorDirection"/> must be set manually
        /// when a pulse ends.
        /// </summary>
        /// <param name="duration">pulse duration in seconds</param>
        /// <returns></returns>
        public Result PrepareForShortPulse(double duration)
        {
            if (duration <= 0 || MAX_SHORT_PULSE_DURATION < duration)
            {
                return new Result() { Error = Error.InvalidData, Reason = $"Short Pulse must be no longer than {MAX_SHORT_PULSE_DURATION} seconds" };
            }

            var cmdSets = new List<(Register, int)>();

            if (!_isInShortPulseMode)
            {
                cmdSets.Add((Register.HOLD_1, 0));
            }

            var ms = (int)(1000 * duration);
            cmdSets.Add((Register.PULL_IN_1, ms));

            Result result = SetRegisters(cmdSets.ToArray());

            if (result.Error == Error.Success)
            {
                _isInShortPulseMode = true;
            }

            return result;
        }

        /// <summary>
        /// Here, we check available channels (A and B).
        /// Note that there is channel Z that is maintaince output to user/waste,
        /// but we do not need to read it
        /// </summary>
        /// <returns>Error code and description</returns>
        protected override Result Initialize()
        {
            MFCSample sample = new();
            Error error;

            _channels = Channels.None;

            try
            {
                if ((error = ReadChannelValues(Channel.A, out MFCChannel freshAir)) == Error.Success)
                {
                    sample.A = freshAir;
                    _channels |= Channels.A;
                    _freshAir = sample.A.MassFlow;
                }
                if ((error = ReadChannelValues(Channel.B, out MFCChannel odor)) == Error.Success)
                {
                    sample.B = odor;
                    _channels |= Channels.B;
                    _odor = sample.B.MassFlow;
                }
                /*if ((error = ReadValveValues(out bool isValve1Opened, out bool isValve2Opened)) == Error.Success)
                {
                    _odorDirection = OdorFlowsTo.Waste
                        | (isValve1Opened ? OdorFlowsTo.System : OdorFlowsTo.Waste)
                        | (isValve2Opened ? OdorFlowsTo.User : OdorFlowsTo.Waste);
                }*/
            }
            catch (Exception ex)
            {
                Stop();
                return new Result()
                {
                    Error = !IsDebugging ? (Error)ex.HResult : Error.AccessFailed,
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
        OdorFlowsTo _odorDirection = OdorFlowsTo.Waste;
        bool _isInShortPulseMode = false;

        readonly Mutex _mutex = new();     // this is needed to use in lock() only because we cannot use _port to lock when debugging
        readonly MFCEmulator _emulator = MFCEmulator.Instance;

        // constants

        const Channel FRESH_AIR_CHANNEL = Channel.A;
        const Channel ODOR_CHANNEL = Channel.B;
        const Channel OUTPUT_CHANNEL = Channel.Z;

        const double MAX_SHORT_PULSE_DURATION = 60;

        public static readonly string CMD_SET = "s";
        public static readonly string CMD_WRITE_REGISTER = "w";
        public static readonly string CMD_TARE_FLOW = "v";

        public const char DATA_END = '\r';

        const double ODOR_TUBE_LENGTH = 600;       // mm
        const double VALVE_MIXER_TUBE_LENGTH = 27; // mm
        const double MIXED_TUBE_LENGTH = 1200;     // mm
        const double TUBE_R = 2;                   // mm

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

            var response = !IsDebugging ? ReadBytes() : _emulator.EmulateReading(mfcAddr); // _port.ReadLine()

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
                buffer.Pressure = double.Parse(values[1]);
                buffer.Temperature = double.Parse(values[2]);
                buffer.VolumeFlow = double.Parse(values[3]);
                buffer.MassFlow = double.Parse(values[4]);
                buffer.Setpoint = double.Parse(values[5]);
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
        /*
        Error ReadValveValues(out bool isValve1Opened, out bool isValve2Opened)
        {
            isValve1Opened = false;
            isValve2Opened = false;

            var mfcAddr = Channel.Z.ToString()[0];

            if (!IsDebugging)
            {
                char[] chars = new char[2] { mfcAddr, DATA_END };
                _port.Write(chars, 0, 2);
            }
            if (_error != null)
            {
                return (Error)Marshal.GetLastWin32Error();
            }
            
            var response = !IsDebugging ? ReadBytes() : _emulator.EmulateReading(mfcAddr);

            if (_error != null)
            {
                return (Error)Marshal.GetLastWin32Error();
            }
            if (string.IsNullOrEmpty(response))
            {
                return Error.ReadFault;
            }

            string[] values = response.Split(' ');
            if (values.Length != 3)
            {
                return Error.BadDataFormat;
            }
            if (values[0][0] != mfcAddr)
            {
                return Error.WrongDevice;
            }
            
            isValve1Opened = values[1][0] == '1';
            isValve2Opened = values[2][0] == '1';

            return Error.Success;
        }*/

        /// <summary>
        /// Sets MFC registers of the output channel
        /// </summary>
        /// <param name="register">regsiter to set</param>
        /// <param name="value">value to pass</param>
        /// <returns>command execution result</returns>
        Result SetRegister(Register register, int value)
        {
            return SetRegisters((register, value));
        }

        /// <summary>
        /// Sets MFC registers of the output channel
        /// </summary>
        /// <param name="sets">a list of (register, value) pairs</param>
        /// <returns>command execution result</returns>
        Result SetRegisters(params (Register register, int value)[] sets)
        {
            var commands = sets.Select(set => $"{char.ToLower((char)OUTPUT_CHANNEL)}{CMD_WRITE_REGISTER}{(int)set.register}={set.value}");
            var result = SendCommands(commands.ToArray());
            CommandResult?.Invoke(this, new CommandResultArgs(string.Join(";", commands), "", result));
            return result;
        }

        /// <summary>
        /// Sends a specific command to the port.
        /// All parameters are simply concatenated in the order they appear
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

            return SendCommands(new string[] { char.ToLower((char)channel) + cmd + value });
        }

        /// <summary>
        /// Sends several commands to the port at once
        /// </summary>
        /// <param name="commands">commands</param>
        /// <returns>Error type and description</returns>
        Result SendCommands(string[] commands)
        {
            Error error;
            string reason;

            lock (_mutex)
            {
                var command = string.Join(DATA_END, commands);
                var readResponse = command[0] != (char)OUTPUT_CHANNEL;
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

                // we should have a response to some of out requests
                if (!IsDebugging && readResponse)
                {
                    // Thread.Sleep(50);

                    // if (_port.BytesToRead > 0)
                    // {
                    string response = ReadBytes();
                    if (commands.Length > 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RESP] [N={response.Length}] {response}");
                    }
                    if (_error != null)
                    {
                        error = (Error)Marshal.GetLastWin32Error();
                    }
                    // }
                }
            }

            return new Result()
            {
                Error = error,
                Reason = reason.Replace(DATA_END, ';')
            };
        }

        /// <summary>
        /// Reads bytes one by one until DATA_END is met or time is out
        /// </summary>
        /// <returns>The string read from the port</returns>
        string ReadBytes()
        {
            string response = "";

            //var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                //* this works with _port.NewLine = "\r"
                response = _port.ReadLine();
                /*/
                int duration = 0;
                int lastChar;

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
                */
            }
            catch
            {
                response = "";
            }

            //System.Diagnostics.Debug.WriteLine($"MFC {response} " + sw.ElapsedMilliseconds.ToString());
            //sw.Stop();

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
                    _emulator.EmulateWriting(bytes);
                }
            }
            catch
            {
                result = Error.WriteFault;
            }

            return result;
        }
    }
}
