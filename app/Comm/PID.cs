using System;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutOlD2Ch.Comm;

public struct PIDSample : ISample             // Sample record/vector; all the measured values for one second
{
    public long Time { get; set; }            // Sample time; milliseconds from start

    public double PID { get; set; }           // PID value in mV
    public double PID_PPM { get; set; }       // PID value as ppm
    public double Loop { get; set; }          // Current loop input in mA
    public double Input { get; set; }         // 10V scaled input
    public double Light { get; set; }         // 10V scaled input
    public double Temperature { get; set; }   // degrees in C

    public double MainValue => PID;

    public override string ToString() => string.Join('\t', new string[] {
        Time.ToString(),
        PID.ToString("F1"),
        PID_PPM.ToString("F2"),
        Loop.ToString("F4"),
        Input.ToString("F2"),
        Light.ToString("F0"),
        Temperature.ToString("F1"),
    });

    public static string[] Header => new string[] {
        "Time",
        "PID mV",
        "PID PPM",
        "loop mA",
        "Input",
        "Light",
        "Temp C"
    };
}

public class PID : CommPort<PIDSample>
{
    public static PID Instance => _instance ??= new();

    public new bool IsDebugging
    {
        get => base.IsDebugging;
        set
        {
            base.IsDebugging = value;
            if (value)
            {
                _emulator = PIDEmulator.Instance;
            }
        }
    }
    public override string Name => "PID";
    public override string[] DataColumns => PIDSample.Header;

    public double Value => _emulator?.PID ?? _lastSample.PID;

    /// <summary>
    /// Private constructor, use Instance property to get the instance.
    /// Needs only to set the port stops bits
    /// </summary>
    private PID() : base()
    {
        _portStopBits = StopBits.Two;
    }

    /// <summary>
    /// In addition closing the port, we have to try to turn the lamp off (this will fail, if the PID SDK wasn't connected)
    /// </summary>
    public override void Stop()
    {
        if (_port != null)
        {
            try
            {
                EnableLamp(false);
            }
            catch { }
        }

        base.Stop();
    }

    /// <summary>
    /// Read data from the port
    /// </summary>
    /// <param name="sample">Buffer to store data</param>
    /// <returns>Error code and description</returns>
    public override Result GetSample(out PIDSample sample)
    {
        sample = new PIDSample();

        if (!IsOpen)
        {
            return new Result()
            {
                Error = Error.NotReady,
                Reason = "The port is not open yet"
            };
        }

        Error error;
        try
        {
            error = ReadValues(out sample);
        }
        catch (Exception ex)
        {
            Stop();
            return new Result()
            {
                Error = !IsDebugging ? (Error)ex.HResult : Error.AccessFailed,
                Reason = "PID IO error: " + ex.Message
            };
        }

        if (error != Error.Success)
        {
            Stop();
            return new Result()
            {
                Error = error,
                Reason = "Issues in communicating the port"
            };
        }
        else
        {
            sample.Time = Utils.Timestamp.Ms;
            _lastSample = sample;
        }

        _error = null;

        return new Result()
        {
            Error = Error.Success,
            Reason = "Read values successfully"
        };
    }

    /// <summary>
    /// Here, we try to turn the PID lamp on
    /// </summary>
    /// <returns>Error code and description</returns>
    protected override Result Initialize()
    {
        try
        {
            Error error;
            if ((error = EnableLamp(true)) != Error.Success)
            {
                Stop();
                return new Result()
                {
                    Error = error,
                    Reason = "Cannot turn the PID lamp on."
                };
            }
        }
        catch (Exception ex)
        {
            Stop();
            return new Result()
            {
                Error = (Error)ex.HResult,
                Reason = "PID lamp IO error: " + ex.Message
            };
        }

        return new Result()
        {
            Error = Error.Success,
            Reason = "The port opened successfully, PID lamp was turned on."
        };
    }


    // Internal

    static PID? _instance;

    PIDEmulator? _emulator;
    PIDSample _lastSample = new();

    [StructLayout(LayoutKind.Explicit)]
    internal struct BtoW                                                    // For convenient byte-level manipulation of 16b integers,
    {                                                                       // mainly for ModBus byte swaps.
        [FieldOffset(0)]
        public byte B0;
        [FieldOffset(1)]
        public byte B1;
        [FieldOffset(0)]
        public ushort W;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct BtoD                                                    // For convenient byte-level manipulation of 32b integers and floats
    {                                                                       // mainly for ModBus byte swaps.
        [FieldOffset(0)]
        public byte B0;
        [FieldOffset(1)]
        public byte B1;
        [FieldOffset(2)]
        public byte B2;
        [FieldOffset(3)]
        public byte B3;
        [FieldOffset(0)]
        public ushort W0;
        [FieldOffset(2)]
        public ushort W1;
        [FieldOffset(0)]
        public uint D;
        [FieldOffset(0)]
        public float f;
    }

    // Lamp read/write

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ModQueryPreset1Regs                                     // ModBus "preset multiple regs" query for ONE register
    {
        public byte SlaveAddr;                                              // Slave address
        public byte Function;                                               // Function number, must be 0x10
        public byte AddressHi;                                              // Starting register address, high byte
        public byte AddressLo;                                              // Starting register address, low byte
        public byte RegCountHi;                                             // Number of registers to write, high byte, must be 0x00
        public byte RegCountLo;                                             // Number of registers to write, low byte, must be 0x01
        public byte ByteCount;                                              // Number of data bytes to follow, must be 0x02
        public byte DataHi;                                                 // Register to be written, high byte
        public byte DataLo;                                                 // Register to be written, low byte

        public byte CRCHi;                                                  // CRC high byte
        public byte CRCLo;                                                  // CRC low byte
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ModResponsePresetRegs                                   // ModBus "preset multiple regs" response
    {                                                                       // (echoes the content of query packet)
        public byte SlaveAddr;                                              // Slave address
        public byte Function;                                               // Function number
        public byte AddressHi;                                              // Starting register address, high byte
        public byte AddressLo;                                              // Starting register address, low byte
        public byte RegCountHi;                                             // Number of registers written, high byte
        public byte RegCountLo;                                             // Number of registers written, low byte

        public byte CRCHi;                                                  // CRC high byte
        public byte CRCLo;                                                  // CRC low byte
    }

    // Data read/write

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ModQueryReadInputRegs                                   // ModBus "read input regs" query
    {
        public byte SlaveAddr;                                              // Slave address
        public byte Function;                                               // Function number, must be 0x04
        public byte AddressHi;                                              // Starting register address, high byte
        public byte AddressLo;                                              // Starting register address, low byte
        public byte NRegsHi;                                                // Number of registers to read, high byte
        public byte NRegsLo;                                                // Number of registers to read, low byte

        public byte CRCHi;                                                  // CRC high byte
        public byte CRCLo;                                                  // CRC low byte
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ModResponseRead12Regs                                   // ModBus "read input regs" response for TWELVE registers
    {
        public byte SlaveAddr;                                              // Slave address, echoing the query address
        public byte Function;                                               // Function number (will be 0x04)
        public byte ByteCount;                                              // Number of bytes in response

        // Each value in MODBUSREG_ADCMVGRP and MODBUSREG_SIGNALGRP is 2x16b = 32b
        // ADCM (mcV)     SIGNAL
        public BtoD RegsRTD;                                                // RTD            Temp (C/F)
        public BtoD Regs10VRef;                                             // 10V ref        scaled input
        public BtoD RegsPID;                                                // PID            ppm
        public BtoD RegsLight;                                              // light          1.0=on, 0.0=off
        public BtoD RegsTemp;                                               // internal temp  (C)
        public BtoD RegsCurrLoop;                                           // the current loop input (4..20mA)

        public byte CRCHi;                                                  // CRC high byte
        public byte CRCLo;                                                  // CRC low byte
    }

    internal const byte MODBUS_ADDR_PID = 0x01;                    // PID SDK ModBus device address
    internal const byte MODBUS_FN_READ_INPUT_REGS = 0x04;          // ModBus function: read (multiple) input registers
    internal const byte MODBUS_FN_PRESET_INPUT_REGS = 0x10;        // ModBus function: preset multiple 
    internal const ushort MODBUS_REG_PID_POWER = 0x1248;           // SDK register (16b) address for PID power enable; LSb enables the PID lamp
    internal const ushort MODBUS_REG_LOOP_POWER = 0x1400;          // SDK register address for the 4...20mA loop power enable; LSb enables the loop
    internal const ushort MODBUS_REG_ADCMV_GROUP = 0x1260;         // SDK register address for the ADCMV group; contains 6 DWORDs
    internal const ushort MODBUS_REG_SIGNAL_GROUP = 0x12C0;        // SDK register address for the SIGNAL group; contains 6 floats
    internal const byte MODBUS_GROUP_LEN = 6;                      // The 6 DWORDs/floats above


    /// <summary>
    /// Before measuring can start, the PID SDK sensor/lamp must
    /// be turned on. However, the manufacturer's documentation does
    /// not explain this; the relevant register was found with
    /// a packet sniffer and it is not clear whether simply setting
    /// one bit in one register is enough - the PID SDK software
    /// sends many other commands on init. Well, at least it seems
    /// to be enough.
    ///
    /// It would also be desirable to turn on the current loop receiver
    /// power supply, but apparently this requires more than just setting
    /// one register; writing 0x0001 to MODBUSREG_LOOPPOWER can crash the
    /// SDK software(embedded and PC). It's not guaranteed to do so,
    /// though.
    /// </summary>
    /// <param name="enable">State to set</param>
    /// <returns>Error code</returns>
    Error EnableLamp(bool enable)
    {
        Error error;
        ModQueryPreset1Regs query = new()
        {
            SlaveAddr = MODBUS_ADDR_PID,
            Function = MODBUS_FN_PRESET_INPUT_REGS,            // Query for setting registers
            AddressLo = MODBUS_REG_PID_POWER & 0xFF,           // 16b register (start) address
            AddressHi = MODBUS_REG_PID_POWER >> 8,
            RegCountHi = 0x00,
            RegCountLo = 0x01,                                 // Set one register ...
            ByteCount = sizeof(ushort),                        // ... which is 16b long
            DataHi = 0x00,
            DataLo = (byte)(enable ? 0x01 : 0x00),             // LSb = PID lamp enable/disable bit
        };

        if ((error = SendQuery(in query)) != Error.Success ||
            (error = ReceiveReply(out ModResponsePresetRegs response)) != Error.Success)
        {
            return error;
        }

        if (query.SlaveAddr != response.SlaveAddr ||           // Expected response is basically an echo; 
            query.Function != response.Function ||             // check it thoroughly, since this is also a
            query.AddressHi != response.AddressHi ||           // device existence check.
            query.AddressLo != response.AddressLo ||
            query.RegCountHi != response.RegCountHi ||
            query.RegCountLo != response.RegCountLo)
        {
            return Error.InvalidData;
        }

        return Error.Success;
    }


    /// <summary>
    /// Reads PID SDK values.
    /// 
    /// The PID SDK board provides, according to its manual, 5 measurements
    /// (and one 10V reference value). Of these, the LIGHT "measurement" just
    /// indicates that the lamp should be on and apparently the RTD value
    /// is always zero.The internal thermometer works, but it is almost 10C off
    /// due to the heat the SDK board itself produces. The 4...20mA current loop
    /// receiver seemed initially useful, but it has only around 10.3b effective
    /// bits in 4...20mA range. This is rather marginal for a Pt100 + resistance
    /// transmitter. (0.33K resolution, when 100R = 10mA). To make things worse,
    /// it is apparently not possible to enable the input without enabling the
    /// logger. (Solution: short-circuit the SDK loop power enable MOSFET.)
    /// Well, at least the PID input works well.
    /// </summary>
    /// <param name="sample">sample</param>
    /// <returns>Reading result</returns>
    Error ReadValues(out PIDSample sample)
    {
        sample = new PIDSample();

        Error error;

        ModQueryReadInputRegs queryRaw = new()
        {
            SlaveAddr = MODBUS_ADDR_PID,                          // Query for reading multiple registers (scaled ADC values)
            Function = MODBUS_FN_READ_INPUT_REGS,
            AddressLo = MODBUS_REG_ADCMV_GROUP & 0xFF,
            AddressHi = MODBUS_REG_ADCMV_GROUP >> 8,
            NRegsHi = 0x00,
            NRegsLo = MODBUS_GROUP_LEN * sizeof(uint) / sizeof(ushort),    // Read 6 DWORD values -> twice as many registers
        };
        ModQueryReadInputRegs queryScaled = new()
        {
            SlaveAddr = MODBUS_ADDR_PID,
            Function = MODBUS_FN_READ_INPUT_REGS,
            AddressLo = MODBUS_REG_SIGNAL_GROUP & 0xFF,
            AddressHi = MODBUS_REG_SIGNAL_GROUP >> 8,
            NRegsHi = 0x00,
            NRegsLo = MODBUS_GROUP_LEN * sizeof(uint) / sizeof(ushort),    // Read 6 DWORD values -> twice as many registers
        };

        if ((error = SendQuery(in queryRaw)) != Error.Success ||
            (error = ReceiveReply(out ModResponseRead12Regs responseRaw)) != Error.Success ||
            (error = SendQuery(in queryScaled)) != Error.Success ||
            (error = ReceiveReply(out ModResponseRead12Regs responseScaled)) != Error.Success)
        {
            return error;
        }

        if (responseRaw.SlaveAddr != MODBUS_ADDR_PID ||                // Responses should reflect the request values
            responseRaw.Function != MODBUS_FN_READ_INPUT_REGS ||
            responseRaw.ByteCount != MODBUS_GROUP_LEN * sizeof(uint) ||
            responseScaled.SlaveAddr != MODBUS_ADDR_PID ||
            responseScaled.Function != MODBUS_FN_READ_INPUT_REGS ||
            responseScaled.ByteCount != MODBUS_GROUP_LEN * sizeof(uint))
        {
            return Error.InvalidData;
        }

        // Reorder the incoming data in place
        // to get little endian values.
        responseRaw.Regs10VRef = ModbusDWORDByteSwap(ref responseRaw.Regs10VRef);
        responseRaw.RegsCurrLoop = ModbusDWORDByteSwap(ref responseRaw.RegsCurrLoop);
        responseRaw.RegsLight = ModbusDWORDByteSwap(ref responseRaw.RegsLight);
        responseRaw.RegsPID = ModbusDWORDByteSwap(ref responseRaw.RegsPID);
        responseRaw.RegsRTD = ModbusDWORDByteSwap(ref responseRaw.RegsRTD);
        responseRaw.RegsTemp = ModbusDWORDByteSwap(ref responseRaw.RegsTemp);

        responseScaled.Regs10VRef = ModbusDWORDByteSwap(ref responseScaled.Regs10VRef);
        responseScaled.RegsCurrLoop = ModbusDWORDByteSwap(ref responseScaled.RegsCurrLoop);
        responseScaled.RegsLight = ModbusDWORDByteSwap(ref responseScaled.RegsLight);
        responseScaled.RegsPID = ModbusDWORDByteSwap(ref responseScaled.RegsPID);
        responseScaled.RegsRTD = ModbusDWORDByteSwap(ref responseScaled.RegsRTD);
        responseScaled.RegsTemp = ModbusDWORDByteSwap(ref responseScaled.RegsTemp);

        sample.PID = responseRaw.RegsPID.D;
        if (responseRaw.Regs10VRef.D != 0)                // 10000mV reference available; convert the PID value to millivolts
        {
            sample.PID = sample.PID / responseRaw.Regs10VRef.D * 10000.0;
        }

        sample.Loop = responseScaled.RegsCurrLoop.f;
        sample.PID_PPM = responseScaled.RegsPID.f;
        sample.Input = responseScaled.Regs10VRef.f;
        sample.Light = responseScaled.RegsLight.f;
        sample.Temperature = responseScaled.RegsTemp.f;

        return Error.Success;
    }

    /// <summary>
    /// Send a query to the PID SDK board.
    /// 
    /// Query must have space for the checksum field; 
    /// This method updates it before transmitting the packet.
    /// </summary>
    /// <typeparam name="T">Type of structure to send to the port</typeparam>
    /// <param name="query">ModBus packet</param>
    /// <returns>Error code</returns>
    Error SendQuery<T>(in T query)
    {
        byte[] bytes = ToBytes(query);
        int length = Marshal.SizeOf(query);

        var u = new BtoW();

        Thread.Sleep(3);                                                // Guarantees sufficient spacing between commands;
                                                                        // shouldn't be actually needed.
        u.W = CRC16(bytes, length - sizeof(ushort));                    // Append checksum; always the last two bytes.
        bytes[length - sizeof(ushort)] = u.B1;                          // Checksum is big-endian
        bytes[length - sizeof(byte)] = u.B0;

        if (!IsDebugging)
        {
            _port.Write(bytes, 0, length);
        }
        else
        {
            _emulator?.EmulateWrite(query);
        }
        if (_error != null)
        {
            return (Error)Marshal.GetLastWin32Error();
        }

        return Error.Success;
    }

    /// <summary>
    /// Receive a ModBus packet from PID SDK.
    /// </summary>
    /// <typeparam name="T">Type of structure to read from the port</typeparam>
    /// <param name="reply">buffer</param>
    /// <returns>Success, if a valid packet was received. 
    /// CRC, if the received packet fails the
    /// Timeout if a valid packet is not received in PORT_TIMEOUT milliseconds
    /// </returns>
    Error ReceiveReply<T>(out T reply) where T : new()
    {
        reply = new T();

        var length = Marshal.SizeOf(reply);
        var u = new BtoW();

        //var sw = System.Diagnostics.Stopwatch.StartNew();

        /* this does not work even with _port.NewLine = "\r";
        var received = _port.ReadLine();
        var buffer = System.Text.Encoding.ASCII.GetBytes(received);
        /*/

        byte[] buffer = new byte[length];

        int duration = 0;
        int bytesRemaining = length;
        int offset = 0;

        // Try to receive 'length' bytes; wait more data in POLL_PERIOD pieces;
        for (; duration <= PORT_TIMEOUT && bytesRemaining > 0; duration += POLL_PERIOD)
        {
            int readCount = !IsDebugging
                ? _port.Read(buffer, offset, bytesRemaining)
                : _emulator?.EmulateReading(buffer, offset, bytesRemaining) ?? 0;
            if (_error != null)           // return immediately (with error) if port read fails.
            {
                return (Error)Marshal.GetLastWin32Error();
            }

            bytesRemaining -= readCount;
            offset += readCount;

            if (bytesRemaining > 0)
            {
                Thread.Sleep(POLL_PERIOD);
            }
        }

        if (bytesRemaining > 0)
        {
            return Error.Timeout;                    // Some data still missing -> operation timed out
        }
        //*/

        //System.Diagnostics.Debug.WriteLine("PID " + sw.ElapsedMilliseconds.ToString());
        //sw.Stop();

        // Calculate checksum and compare it to the received one
        u.W = CRC16(buffer, length - sizeof(ushort));

        if (u.B0 != buffer[length - sizeof(byte)] ||
            u.B1 != buffer[length - sizeof(ushort)])
        {
            return Error.CRC;
        }

        reply = FromBytes<T>(buffer);
        return Error.Success;                                        // Packet received without errors
    }

    // Utilities

    static readonly byte[] CRC_DATA_HI = new byte[256]
                            { 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                              0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                              0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
                              0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
                              0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81,
                              0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                              0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
                              0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
                              0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                              0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                              0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
                              0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                              0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                              0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                              0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
                              0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                              0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                              0x40 };

    static readonly byte[] CRC_DATA_LO = new byte[256]
                            { 0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7, 0x05, 0xC5, 0xC4,
                              0x04, 0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E, 0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09,
                              0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9, 0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD,
                              0x1D, 0x1C, 0xDC, 0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
                              0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32, 0x36, 0xF6, 0xF7,
                              0x37, 0xF5, 0x35, 0x34, 0xF4, 0x3C, 0xFC, 0xFD, 0x3D, 0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A,
                              0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38, 0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE,
                              0x2E, 0x2F, 0xEF, 0x2D, 0xED, 0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
                              0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60, 0x61, 0xA1, 0x63, 0xA3, 0xA2,
                              0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4, 0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F,
                              0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB, 0x69, 0xA9, 0xA8, 0x68, 0x78, 0xB8, 0xB9, 0x79, 0xBB,
                              0x7B, 0x7A, 0xBA, 0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
                              0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0, 0x50, 0x90, 0x91,
                              0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C,
                              0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E, 0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98, 0x88,
                              0x48, 0x49, 0x89, 0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
                              0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83, 0x41, 0x81, 0x80,
                              0x40 };

    /// <summary>
    /// ModBus CRC algorithm, lifted from "Modicon ModBus Protocol Reference Guide",
    /// PI-MBUS-300 Rev.J., MODICON Inc., 1996.
    /// </summary>
    /// <param name="data">Message</param>
    /// <param name="length">number of bytes of data to use</param>
    /// <returns>16b CRC to be appended to the message</returns>
    internal static ushort CRC16(byte[] data, int length)
    {
        int i = 0;
        byte crcHi = 0xFF;
        byte crcLo = 0xFF;

        while (length-- > 0)
        {
            byte index = (byte)(crcHi ^ data[i++]);
            crcHi = (byte)(crcLo ^ CRC_DATA_HI[index]);
            crcLo = CRC_DATA_LO[index];
        }

        return (ushort)((crcHi << 8) | crcLo);
    }

    /// <summary>
    /// "Fixes" the byte ordering of 32b records received from PID SDK.
    /// 
    /// PID SDK uses a weird mixture of big endian and little endian: the
    /// two WORDs forming the 32b value are in big endian format, but they're
    /// sent out lower WORD first.
    /// </summary>
    /// <param name="dword">32b</param>
    /// <returns>Fixed 32b</returns>
    static BtoD ModbusDWORDByteSwap(ref BtoD dword) => new()
    {
        B0 = dword.B1,
        B1 = dword.B0,
        B2 = dword.B3,
        B3 = dword.B2,
    };

    /// <summary>
    /// Makes an array of bytes out of a structure
    /// </summary>
    /// <typeparam name="T">Structure type to convert to bytes</typeparam>
    /// <param name="str">Structure instance</param>
    /// <returns>Arrays of bytes</returns>
    static byte[] ToBytes<T>(T str)
    {
        int size = Marshal.SizeOf(str);
        byte[] bytes = new byte[size];

        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(str!, ptr, true);
        Marshal.Copy(ptr, bytes, 0, size);
        Marshal.FreeHGlobal(ptr);

        return bytes;
    }

    /// <summary>
    /// Makes a structure out of array of bytes
    /// </summary>
    /// <typeparam name="T">Structure type to restore from bytes</typeparam>
    /// <param name="bytes">Array of bytes</param>
    /// <returns>Structure instance</returns>
    static T FromBytes<T>(byte[] bytes) where T : new()
    {
        var str = new T();

        int size = Marshal.SizeOf(str);
        IntPtr ptr = Marshal.AllocHGlobal(size);

        Marshal.Copy(bytes, 0, ptr, size);

        var s = (T?)Marshal.PtrToStructure(ptr, str.GetType());
        if (s != null)
            str = s;

        Marshal.FreeHGlobal(ptr);

        return str;
    }
}
