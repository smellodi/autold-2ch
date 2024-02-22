using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutOlD2Ch.Comm;

public struct MFCChannel
{
    public double Pressure { get; set; }
    public double Temperature { get; set; }
    public double VolumeFlow { get; set; }
    public double MassFlow { get; set; }
    public double Setpoint { get; set; }
    public string Gas { get; set; }

    public readonly string ToString(char separator) => string.Join(separator, new string[] {
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
    /// Odored air channel #1
    /// </summary>
    public MFCChannel B { get; set; }

    /// <summary>
    /// Odored air channel #2
    /// </summary>
    public MFCChannel C { get; set; }

    public readonly double MainValue => C.MassFlow;

    public override readonly string ToString() => string.Join('\t', new string[] {
        Time.ToString(),
        A.ToString('\t'),
        B.ToString('\t'),
        C.ToString('\t'),
    });

    public static string[] Header
    {
        get
        {
            var list = new List<string>();
            list.AddRange(new string[] { "", "|", "A", "", "|", "B", "", "|", "C", "", "|\r\nTime" });
            list.AddRange(MFCChannel.Header);       // A
            list.AddRange(MFCChannel.Header);       // B
            list.AddRange(MFCChannel.Header);       // C
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
        C = 'c',
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
    public enum ValvesOpened
    {
        None = 0,
        Valve1 = 10,
        Valve2 = 1,
        All = Valve1 | Valve2,
    }

    public event EventHandler<CommandResultArgs>? CommandResult;
    public event EventHandler<string> Message = delegate { };

    public override string Name => "MFC";
    public override string[] DataColumns => MFCSample.Header;

    public const double ODOR_MAX_SPEED = 90.0;
    public const double ODOR_MIN_SPEED = 0.0;

    public const double MAX_SHORT_PULSE_DURATION = 0xffff; // ms

    public static readonly string CMD_SET = "s";
    public static readonly string CMD_WRITE_REGISTER = "w";
    public static readonly string CMD_TARE_FLOW = "v";

    public const char DATA_END = '\r';


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
    /// Odor #1 flow speed, in milliliters per minute
    /// </summary>
    public double Odor1Speed
    {
        get => _odor1;
        set
        {
            var val = value.ToString("F1").Replace(',', '.');
            var result = SendCommand(ODOR1_CHANNEL, CMD_SET, val);
            if (result.Error == Error.Success)
            {
                _odor1 = value;
            }
            CommandResult?.Invoke(this, new CommandResultArgs(ODOR1_CHANNEL + CMD_SET, val, result));
        }
    }

    /// <summary>
    /// Odor #2 flow speed, in milliliters per minute
    /// </summary>
    public double Odor2Speed
    {
        get => _odor2;
        set
        {
            var val = value.ToString("F1").Replace(',', '.');
            var result = SendCommand(ODOR2_CHANNEL, CMD_SET, val);
            if (result.Error == Error.Success)
            {
                _odor2 = value;
            }
            CommandResult?.Invoke(this, new CommandResultArgs(ODOR2_CHANNEL + CMD_SET, val, result));
        }
    }

    public ValvesOpened OdorDirection
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
                _isInShortPulseMode = SetRegisters((Register.HOLD_0, 0)).Error == Error.Success && SetRegisters((Register.HOLD_1, 0)).Error == Error.Success;
            }
            else if (!value && _isInShortPulseMode)
            {/*
                _isInShortPulseMode =
                    !(SetRegisters((Register.HOLD_0, 255), (Register.PULL_IN_0, 0)).Error == Error.Success) &&
                    !(SetRegisters((Register.HOLD_1, 255), (Register.PULL_IN_1, 0)).Error == Error.Success);*/
                var result = SetRegisters((Register.HOLD_0, 255), (Register.PULL_IN_0, 0), (Register.HOLD_1, 255), (Register.PULL_IN_1, 0));
                _isInShortPulseMode = !(result.Error == Error.Success);
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
            Odor1Speed = ODOR_MIN_SPEED;
            Odor2Speed = ODOR_MIN_SPEED;
        }

        base.Stop();
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
                if (_channels.HasFlag(Channels.A))
                {
                    error = ReadChannelValues(Channel.A, out MFCChannel freshAir);
                    sample.A = freshAir;
                }

                if (error == Error.Success && _channels.HasFlag(Channels.B))
                {
                    error = ReadChannelValues(Channel.B, out MFCChannel odor1);
                    sample.B = odor1;
                }

                if (error == Error.Success && _channels.HasFlag(Channels.C))
                {
                    error = ReadChannelValues(Channel.C, out MFCChannel odor2);
                    sample.C = odor2;
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

        return new Result()
        {
            Error = error,
            Reason = reason
        };
    }

    /// <summary>
    /// Prepares the output valve for a short flow pulse to the user using the valves controller timer,
    /// then executes the both pulses in sync.
    /// Note that even though the valve(s) is/are closed automatically, the <see cref="OdorDirection"/> must be set manually
    /// when a pulse ends.
    /// </summary>
    /// <param name="duration1">channel #1 pulse duration, ms. Must be shorter than <see cref="MAX_SHORT_PULSE_DURATION"/>.</param>
    /// <param name="duration2">channel #2 pulse duration, ms. Must be shorter than <see cref="MAX_SHORT_PULSE_DURATION"/>.</param>
    /// <returns></returns>
    public Result StartPulses(int duration1, int duration2)
    {
        if (duration1 < 0 || MAX_SHORT_PULSE_DURATION < duration1 ||
            duration2 < 0 || MAX_SHORT_PULSE_DURATION < duration2)
        {
            return new Result()
            {
                Error = Error.InvalidData,
                Reason = $"Pulse must be no longer than {MAX_SHORT_PULSE_DURATION} ms"
            };
        }

        var cmdSets = new List<(Register, int)>();

        if (!_isInShortPulseMode)
        {
            cmdSets.Add((Register.HOLD_0, 0));
            cmdSets.Add((Register.HOLD_1, 0));
        }

        if (duration1 > 0)
        {
            cmdSets.Add((Register.PULL_IN_0, duration1));
        }
        if (duration2 > 0)
        {
            cmdSets.Add((Register.PULL_IN_1, duration2));
        }

        Result result = SetRegisters(cmdSets.ToArray());

        if (result.Error == Error.Success)
        {
            _isInShortPulseMode = true;
        }

        return result;
    }

    /// <summary>
    /// Prepares the output valve for a short flow pulse to the user using the valves controller timer,
    /// then executes the pulse.
    /// Note that even though the valve is closed automatically, the <see cref="OdorDirection"/> must be set manually
    /// when a pulse ends.
    /// </summary>
    /// <param name="valve">the valve to open.</param>
    /// <param name="duration2">channel #2 pulse duration, ms. Must be shorter than <see cref="MAX_SHORT_PULSE_DURATION"/>.</param>
    /// <returns></returns>
    public Result StartPulse(ValvesOpened valve, int duration)
    {
        if (duration <= 0 || MAX_SHORT_PULSE_DURATION < duration)
        {
            return new Result()
            {
                Error = Error.InvalidData,
                Reason = $"Pulse must be no longer than {MAX_SHORT_PULSE_DURATION} ms"
            };
        }

        var cmdSets = new List<(Register, int)>();

        if (!_isInShortPulseMode)
        {
            cmdSets.Add((Register.HOLD_0, 0));
            cmdSets.Add((Register.HOLD_1, 0));
        }

        cmdSets.Add((valve == ValvesOpened.Valve1 ? Register.PULL_IN_0 : Register.PULL_IN_1, duration));

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
            if ((error = ReadChannelValues(Channel.B, out MFCChannel odor1)) == Error.Success)
            {
                sample.B = odor1;
                _channels |= Channels.B;
                _odor1 = sample.B.MassFlow;
            }
            if ((error = ReadChannelValues(Channel.C, out MFCChannel odor2)) == Error.Success)
            {
                sample.C = odor2;
                _channels |= Channels.C;
                _odor2 = sample.C.MassFlow;
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
        C = 4,
        All = A | B | C,
    }

    static MFC? _instance;

    Channels _channels = Channels.None;

    double _freshAir = 5.0;
    double _odor1 = 4.0;
    double _odor2 = 4.0;
    ValvesOpened _odorDirection = ValvesOpened.None;
    bool _isInShortPulseMode = false;

    readonly Mutex _mutex = new();     // this is needed to use in lock() only because we cannot use _port to lock when debugging
    readonly MFCEmulator _emulator = MFCEmulator.Instance;

    // constants

    const Channel FRESH_AIR_CHANNEL = Channel.A;
    const Channel ODOR1_CHANNEL = Channel.B;
    const Channel ODOR2_CHANNEL = Channel.C;
    const Channel OUTPUT_CHANNEL = Channel.Z;

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
            _port?.Write(chars, 0, 2);
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
        string response;

        //string response = "";
        //var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            //* this works with _port.NewLine = "\r"
            response = _port?.ReadLine() ?? "";
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
                _port?.Write(bytes, 0, bytes.Length);
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
