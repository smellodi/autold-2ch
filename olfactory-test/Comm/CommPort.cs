using System;
using System.Runtime.InteropServices;
using System.IO.Ports;

namespace Olfactory.Comm
{
    /// <summary>
    /// A subset of Windows ERROR_XXX codes that are used to return from methods
    /// </summary>
    public enum Error
    {
        Success = 0,                    // ERROR_SUCCESS, all is OK
        InvalidFunction = 0x01,         // ERROR_INVALID_FUNCTION
        BadDataFormat = 0x0B,           // ERROR_BAD_FORMAT
        WrongDevice = 0x0C,             // ERROR_INVALID_ACCESS
        InvalidData = 0x0D,             // ERROR_INVALID_DATA
        NotReady = 0x15,                // ERROR_NOT_READY, the port is not open yer/already
        CRC = 0x17,                     // ERROR_CRC
        WriteFault = 0x1D,              // ERROR_WRITE_FAULT, writing to port has failed
        ReadFault = 0x1E,               // ERROR_READ_FAULT, reading from port has failed
        AccessFailed = 0x1F,            // ERROR_GEN_FAILURE, ports were available, but access was not successful
        OpenFailed = 0x6E,              // ERROR_OPEN_FAILED, not succeeded to open a port, 
        Timeout = 0x5B4,                // ERROR_TIMEOUT
    }

    /// <summary>
    /// We use this (error, reason) pair to return from public methods
    /// </summary>
    public class Result
    {
        public Error Error;
        public string Reason;

        public override string ToString()
        {
            return "0x" + ((int)Error).ToString("X4") + $" ({Error}): {Reason}";
        }
    }


    /// <summary>
    /// MFC and PID inherit this class that declares the reading method
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class CommPort<T> : CommPort where T : struct
    {
        /// <summary>
        /// Retrieving data sample from port
        /// </summary>
        /// <param name="sample">Sample to be filled with values</param>
        /// <returns>Error code and description</returns>
        public abstract Result GetSample(out T sample);
    }

    /// <summary>
    /// COM port functionality common for both MFC and PID
    /// </summary>
    public abstract class CommPort
    {
        /// <summary>
        /// Fires when COM port is closed
        /// </summary>
        public virtual event EventHandler Closed = delegate { };

        /// <summary>
        /// Fires when high-level error (Sistem.IO.Ports.SerialPort) is received from COM port
        /// </summary>
        public virtual event EventHandler<Result> RequestResult = delegate { };

        public bool IsOpen { get; protected set; } = false;
        public bool IsDebugging { get; set; } = false;
        public abstract string Name { get; }

        /// <summary>
        /// Header for logging output, witch a list of column names
        /// </summary>
        public abstract string[] DataColumns { get; }


        /// <summary>
        /// Closes the communications ports
        /// </summary>
        public virtual void Stop()
        {
            if (_port != null)
            {
                try { _port.Close(); }                               // Port were in use? Simply close handle.
                finally { _port = null; }
            }

            if (IsOpen)
            {
                IsOpen = false;
                Closed(this, new EventArgs());
            }
        }

        /// <summary>
        /// Opens the communication port
        /// </summary>
        /// <param name="comID">1..255</param>
        /// <returns>Error code and description</returns>
        public Result Start(int comID)
        {
            return Start((0 < comID && comID < PORT_MAX_ID) ? $"COM{comID}" : "");
        }

        /// <summary>
        /// Opens the communication port
        /// </summary>
        /// <param name="portName">COM1..COM255</param>
        /// <returns>Error code and description</returns>
        public Result Start(string portName)
        {
            _error = null;

            if (string.IsNullOrEmpty(portName))
            {
                return new Result()
                {
                    Error = Error.OpenFailed,
                    Reason = "Port was not specified"
                };
            }

            if (!IsDebugging)
            {
                try
                {
                    _port = OpenSerialPort(portName);
                    _port.ErrorReceived += (s, e) =>
                    {
                        _error = e.EventType;
                        RequestResult(this, new Result()
                        {
                            Error = (Error)Marshal.GetLastWin32Error(),
                            Reason = $"COM internal error ({e.EventType})"
                        });
                    };
                }
                catch (Exception ex)
                {
                    return new Result()
                    {
                        Error = Error.OpenFailed,
                        Reason = ex.Message
                    };
                }

                if (!_port.IsOpen)
                {
                    Stop();
                    return new Result()
                    {
                        Error = Error.OpenFailed,
                        Reason = "The port was created but is still closed"
                    };
                }
            }

            _error = null;

            var result = Initialize();

            IsOpen = result.Error == Error.Success;

            return result;
        }

        // To be overriden

        /// <summary>
        /// Initializes the opened port, so it is ready to deliver data.
        /// It should not throw exceptions. If an error happens, it should close the port.
        /// </summary>
        /// <returns>Erro code and description</returns>
        protected abstract Result Initialize();


        // Internal

        protected SerialPort _port;
        protected SerialError? _error = null;

        protected int _portSpeed = 19200;
        protected Parity _portParity = Parity.None;
        protected StopBits _portStopBits = StopBits.None;

        protected const int PORT_TIMEOUT = 300;              // Timeout value for serial ports, mainly in case a BT port is opened
        protected const int POLL_PERIOD = 20;                // Polling period in ms for reception

        const byte PORT_MAX_ID = 255;                        // Highest possible serial port number

        /// <summary>
        /// Creates and opens a serial port
        /// </summary>
        /// <param name="portName">COM1..COM255</param>
        /// <returns>The port</returns>
        SerialPort OpenSerialPort(string portName)
        {
            var port = new SerialPort(portName);

            port.StopBits = _portStopBits;
            port.Parity = _portParity;
            port.BaudRate = _portSpeed;
            port.DataBits = 8;
            port.DtrEnable = true;
            port.RtsEnable = true;
            port.DiscardNull = false;
            port.WriteTimeout = PORT_TIMEOUT;
            port.ReadTimeout = PORT_TIMEOUT; // int.MaxValue;

            port.Open();

            /*Application.Current.Exit += (s, e) =>
            {
                Stop();
            };*/

            System.Threading.Thread.Sleep(50);       // TODO - maybe, we do not need this

            return port;
        }

        /// <summary>
        /// Creates and opens a serial port
        /// </summary>
        /// <param name="portNumber">1..255</param>
        /// <returns>The port</returns>
        SerialPort OpenSerialPort(int portNumber)
        {
            if (portNumber > PORT_MAX_ID)
            {
                throw new Exception($"Port number cannot exceed {PORT_MAX_ID}");
            }

            return OpenSerialPort($"COM{portNumber}");
        }

    }
}
