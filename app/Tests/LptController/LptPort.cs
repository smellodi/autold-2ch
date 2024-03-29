﻿using AutOlD2Ch.Utils;
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;

namespace AutOlD2Ch.Pages.LptController;

public class LptPort
{
    public string Name { get; init; } = "";
    public int FromAddress { get; init; }
    public int ToAddress { get; init; }

    public int DataAddress => FromAddress;
    public int StatusAddress => FromAddress + 1;
    public int ControlAddress => FromAddress + 2;

    public override string ToString()
    {
        string range = $"0x{FromAddress:x4} - 0x{ToAddress:x4}";
        return $"{Name} ({range})";
    }

    public short ReadData() => Read(FromAddress);
    public short ReadStatus() => Read(FromAddress + 1);
    public short ReadControl() => Read(FromAddress + 2);
    public void WriteData(short value) => Write(FromAddress, value);
    public void WriteControl(short value) => Write(FromAddress + 2, value);

    /// <summary>
    /// Represents all pins in a single integer
    /// </summary>
    /// <returns>0b XX XX XX XX C3 C2 C1 C0 S7 S6 S5 S4 S3 XX XX XX D7 D6 D5 D4 D3 D2 D1 D0</returns>
    public int ReadAll() => (ReadControl() << 16) | ((ReadStatus() & 0xFF) << 8) | (ReadData() & 0xFF);

    /// <summary>
    /// Same as <see cref="ReadAll"/> but with properly inversed pin values
    /// </summary>
    /// <returns>Int32 value with properly inversed bits</returns>
    public int ReadAllAsPins() => ReadAll() ^ 0x000B8000;

    #region Static IO

    const string Platform = "x64"; // "32"

    [DllImport($"inpout{Platform}.dll", EntryPoint = "IsInpOutDriverOpen")]
    public static extern uint IsAvailable();

    [DllImport($"inpout{Platform}.dll", EntryPoint = "Inp32")]
    public static extern short Read(int address);
    [DllImport($"inpout{Platform}.dll", EntryPoint = "Out32")]
    public static extern void Write(int adress, short value);

    [DllImport($"inpout{Platform}.dll", EntryPoint = "DlPortReadPortUchar")]
    public static extern byte ReadUInt8(short PortAddress);
    [DllImport($"inpout{Platform}.dll", EntryPoint = "DlPortWritePortUchar")]
    public static extern void WriteUint8(short PortAddress, byte Data);

    [DllImport($"inpout{Platform}.dll", EntryPoint = "DlPortReadPortUshort")]
    public static extern ushort ReadUInt16(short PortAddress);
    [DllImport($"inpout{Platform}.dll", EntryPoint = "DlPortWritePortUshort")]
    public static extern void WriteUint16(short PortAddress, ushort Data);

    [DllImport($"inpout{Platform}.dll", EntryPoint = "DlPortWritePortUlong")]
    public static extern void WriteUInt32(int PortAddress, uint Data);
    [DllImport($"inpout{Platform}.dll", EntryPoint = "DlPortReadPortUlong")]
    public static extern uint ReadUInt32(int PortAddress);

    #endregion

    public static string VirtualPortClassGuid => "{4d36e978-e325-11ce-bfc1-08002be10318}";

    public static LptPort[] GetPorts()
    {
        var ports = new List<LptPort>();

        //var lptPortSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_ParallelPort");
        var lptPortSearcher = new ManagementObjectSearcher("root\\cimv2",
                $"SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{VirtualPortClassGuid}\"");

        foreach (var lptPort in lptPortSearcher.Get())
        {
            var portName = lptPort.Properties["Name"].Value.ToString() ?? lptPort.ClassPath.ToString();
            if (!portName.Contains("LPT"))
                continue;

            // In addition we need to find the port's start and end adresses that are stored in some PNP resource

            if (!lptPort.TryGetProp("PNPDeviceId", out string searchTerm))
                continue;

            searchTerm = searchTerm.Replace(@"\", @"\\");

            var pnpSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPAllocatedResource");
            foreach (var pnp in pnpSearcher.Get())
            {
                if (!pnp.TryGetProp("dependent", out string dependentValue))
                    continue;
                if (!pnp.TryGetProp("antecedent", out string antecedentValue))
                    continue;

                if (!dependentValue.Contains(searchTerm))
                    continue;

                var portResourceSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PortResource");
                foreach (var portResource in portResourceSearcher.Get())
                {
                    if (portResource.ToString() != antecedentValue)
                        continue;

                    if (!portResource.TryGetProp("StartingAddress", out int startAddress))
                        continue;
                    if (!portResource.TryGetProp("EndingAddress", out int endAddress))
                        continue;

                    ports.Add(new LptPort()
                    {
                        Name = portName,
                        FromAddress = startAddress,
                        ToAddress = endAddress
                    });

                    break;
                }

                break;
            }
        }

        return ports.ToArray();
    }

    public static string PinID(byte bit) => bit switch
    {
        <= 7 => $"D{bit}",
        >= 11 and <= 15 => $"S{bit - 8}",
        >= 16 => $"C{bit - 16}",
        _ => ""
    };

    public static string PinName(byte bit) => bit switch
    {
        0 => "Data0",
        1 => "Data1",
        2 => "Data2",
        3 => "Data3",
        4 => "Data4",
        5 => "Data5",
        6 => "Data6",
        7 => "Data7",
        11 => "Error",
        12 => "Select",
        13 => "PaperEnd",
        14 => "Ack",
        15 => "Busy",
        16 => "Strobe",
        17 => "AutoFeed",
        18 => "Init",
        19 => "SelectInput",
        _ => ""
    };

    #region DATA pins

    /// <summary>
    /// Pin #2 / DATA-0.
    /// </summary>
    public bool D0
    {
        get => GetBit(DataAddress, 0);
        set => SetBit(DataAddress, 0, value);
    }

    /// <summary>
    /// Pin #3 / DATA-1.
    /// </summary>
    public bool D1
    {
        get => GetBit(DataAddress, 1);
        set => SetBit(DataAddress, 1, value);
    }

    /// <summary>
    /// Pin #4 / DATA-2.
    /// </summary>
    public bool D2
    {
        get => GetBit(DataAddress, 2);
        set => SetBit(DataAddress, 2, value);
    }

    /// <summary>
    /// Pin #5 / DATA-3.
    /// </summary>
    public bool D3
    {
        get => GetBit(DataAddress, 3);
        set => SetBit(DataAddress, 3, value);
    }

    /// <summary>
    /// Pin #6 / DATA-4.
    /// </summary>
    public bool D4
    {
        get => GetBit(DataAddress, 4);
        set => SetBit(DataAddress, 4, value);
    }

    /// <summary>
    /// Pin #7 / DATA-5.
    /// </summary>
    public bool D5
    {
        get => GetBit(DataAddress, 5);
        set => SetBit(DataAddress, 5, value);
    }

    /// <summary>
    /// Pin #8 / DATA-6.
    /// </summary>
    public bool D6
    {
        get => GetBit(DataAddress, 6);
        set => SetBit(DataAddress, 6, value);
    }

    /// <summary>
    /// Pin #9 / DATA-7.
    /// </summary>
    public bool D7
    {
        get => GetBit(DataAddress, 7);
        set => SetBit(DataAddress, 7, value);
    }

    #endregion

    #region STATUS pins

    /// <summary>
    /// Pin #15 / ERROR.
    /// </summary>
    public bool S3 => GetBit(StatusAddress, 3);

    /// <summary>
    /// Pin #13 / SEL.
    /// </summary>
    public bool S4 => GetBit(StatusAddress, 4);

    /// <summary>
    /// Pin #12 / PAPEREND.
    /// </summary>
    public bool S5 => GetBit(StatusAddress, 5);

    /// <summary>
    /// Pin #10 / ACK.
    /// </summary>
    public bool S6 => GetBit(StatusAddress, 6);

    /// <summary>
    /// Pin #11 / BUSY.
    /// </summary>
    public bool S7 => !GetBit(StatusAddress, 7);

    #endregion

    #region CONTROL pins

    /// <summary>
    /// Pin #1 / STROBE
    /// </summary>
    public bool C0
    {
        get => !GetBit(ControlAddress, 0);
        set => SetBit(ControlAddress, 0, !value);
    }

    /// <summary>
    /// Pin #14 / AUTOF.
    /// </summary>
    public bool C1
    {
        get => !GetBit(ControlAddress, 1);
        set => SetBit(ControlAddress, 1, !value);
    }

    /// <summary>
    /// Pin #16 / INIT.
    /// </summary>
    public bool C2
    {
        get => GetBit(ControlAddress, 2);
        set => SetBit(ControlAddress, 2, value);
    }

    /// <summary>
    /// Pin #17 / SELIN.
    /// </summary>
    public bool C3
    {
        get => !GetBit(ControlAddress, 3);
        set => SetBit(ControlAddress, 3, !value);
    }

    #endregion

    /// <summary>
    /// Generates a random marker
    /// </summary>
    /// <param name="set">set of available marker. The last marker must be the terminating marker that occurs much more rarely than others</param>
    /// <returns>a marker, or 0</returns>
    public static short Emulate(short[] set)
    {
        if (set.Length == 0)
            return 0;

        var value = _rnd.NextDouble();
        if (value < 0.002)
            return set[^1];
        else if (value < 0.02)
            return set[_rnd.Next(set.Length - 1)];
        else
            return 0;
    }


    // Internal

    readonly static Random _rnd = new();

    private static bool GetBit(int address, byte bit)
    {
        return (Read(address) & (1 << bit)) > 0;
    }

    private static void SetBit(int address, byte bit, bool value)
    {
        short mask = (short)(1 << bit);
        var current = Read(address);
        Write(address, (short)(value ? current | mask : current & (~mask)));
    }
}
