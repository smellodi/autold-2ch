using System;

namespace AutOlD2Ch.Tests.LptController;

internal class ComPort: IDisposable
{
    public ComPort(string name)
    {
        _comPort = new System.IO.Ports.SerialPort(name)
        {
            StopBits = System.IO.Ports.StopBits.One,
            Parity = System.IO.Ports.Parity.None,
            BaudRate = 115200,
            DataBits = 8,
        };
        _comPort.Open();
    }

    public void SendMarker(byte marker)
    {
        var buffer = new byte[] { marker, 0 };
        _comPort.Write(buffer, 0, buffer.Length);
    }

    public void Dispose()
    {
        try { _comPort.Close(); }
        finally { }

        GC.SuppressFinalize(this);
    }

    // Internal

    readonly System.IO.Ports.SerialPort _comPort;
}
