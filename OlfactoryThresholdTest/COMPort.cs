using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Diagnostics;
using System.Windows.Forms;

namespace OlfactoryThresholdTest
{
    public class COMPort
    {

        SerialPort mfcSerialPort;

        bool connectionActive = false;

        public COMPort(int portNumber)
        {
            try
            {
                mfcSerialPort = new SerialPort("COM" + portNumber);
                mfcSerialPort.BaudRate = 19200;
                mfcSerialPort.Parity = Parity.None;
                mfcSerialPort.StopBits = StopBits.One;
                mfcSerialPort.DataBits = 8;
                mfcSerialPort.Handshake = Handshake.None;
                //mfcSerialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                mfcSerialPort.Open();
                connectionActive = true;
            }
            catch (System.IO.IOException e)
            {
                MessageBox.Show("Could not connect to COM" + portNumber);
                connectionActive = false;
            }
        }

        public void setRates(double rateMFC1, double rateMFC2)
        {

            if (mfcSerialPort.IsOpen == true) { 
                Debug.WriteLine("Trying to set new rates: " + rateMFC1 + ", " + rateMFC2);
                mfcSerialPort.Write("as" + rateMFC1.ToString().Replace(",", ".") + "\r");
                mfcSerialPort.Write("bs" + rateMFC2.ToString().Replace(",", ".") + "\r");
                //mfcSerialPort.Write("as5.10\r");
                //mfcSerialPort.Write("bs50.10\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void setOdorSourceOn()
        {

            if (mfcSerialPort.IsOpen == true)
            {
                mfcSerialPort.Write("zs10\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void setOdorSourceOff()
        {

            if (mfcSerialPort.IsOpen == true)
            {
                mfcSerialPort.Write("zs00\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void setOdorToUser()
        {

            if (mfcSerialPort.IsOpen == true)
            {
                mfcSerialPort.Write("zs11\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void setOdorToWaste()
        {

            if (mfcSerialPort.IsOpen == true)
            {
                mfcSerialPort.Write("zs10\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void runStep1(int saturationDelay, int pulseDuration)
        {
            if (mfcSerialPort.IsOpen == true)
            {
                mfcSerialPort.Write("as5.0\r");
                mfcSerialPort.Write("bs4.0\r");
                System.Threading.Thread.Sleep(saturationDelay * 1000);
                mfcSerialPort.Write("zs11\r");
                System.Threading.Thread.Sleep(pulseDuration * 1000);
                mfcSerialPort.Write("zs10\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void runStep2(int saturationDelay, int pulseDuration)
        {
            if (mfcSerialPort.IsOpen == true)
            {
                mfcSerialPort.Write("as5.0\r");
                mfcSerialPort.Write("bs8.0\r");
                System.Threading.Thread.Sleep(saturationDelay * 1000);
                mfcSerialPort.Write("zs11\r");
                System.Threading.Thread.Sleep(pulseDuration * 1000);
                mfcSerialPort.Write("zs10\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void runStep3(int saturationDelay, int pulseDuration)
        {
            if (mfcSerialPort.IsOpen == true)
            {
                mfcSerialPort.Write("as5.0\r");
                mfcSerialPort.Write("bs16.0\r");
                System.Threading.Thread.Sleep(saturationDelay * 1000);
                mfcSerialPort.Write("zs11\r");
                System.Threading.Thread.Sleep(pulseDuration * 1000);
                mfcSerialPort.Write("zs10\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void runStep4(int saturationDelay, int pulseDuration)
        {
            if (mfcSerialPort.IsOpen == true)
            {
                mfcSerialPort.Write("as5.0\r");
                mfcSerialPort.Write("bs32.0\r");
                System.Threading.Thread.Sleep(saturationDelay * 1000);
                mfcSerialPort.Write("zs11\r");
                System.Threading.Thread.Sleep(pulseDuration * 1000);
                mfcSerialPort.Write("zs10\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void runStep5(int saturationDelay, int pulseDuration)
        {
            if (mfcSerialPort.IsOpen == true)
            {
                mfcSerialPort.Write("as5.0\r");
                mfcSerialPort.Write("bs64.0\r");
                System.Threading.Thread.Sleep(saturationDelay * 1000);
                mfcSerialPort.Write("zs11\r");
                System.Threading.Thread.Sleep(pulseDuration * 1000);
                mfcSerialPort.Write("zs10\r");
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        public void setConnectionActive(bool value)
        {
            connectionActive = value;
        }

        public bool getConnectionActive()
        {
            return connectionActive;
        }

    }
}

