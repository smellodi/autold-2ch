using AutOlD2Ch.Utils;
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
            Handshake = System.IO.Ports.Handshake.None,
            BaudRate = 115200,
            DataBits = 8,
        };
        _comPort.Open();
    }

    public void SendMarker(byte marker)
    {
        // set marker
        var buffer = new byte[] { marker };
        _comPort.Write(buffer, 0, buffer.Length);

        // clear marker
        DispatchOnce.Do(0.5, () =>
        {
            var buffer = new byte[] { 0 };
            _comPort.Write(buffer, 0, buffer.Length);
        });
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
