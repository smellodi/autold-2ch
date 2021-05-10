using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OlfactoryThresholdTest
{
    public partial class Interface : Form
    {

        COMPort port;
        int portNumber = 5;
        double rateMFC1 = 5.00;
        double rateMFC2 = 20.00;
        int saturationDelay = 30;
        int pulseDuration = 10;

        public Interface()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {

        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }

        private void button6_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Trying to open COM.");
            port = new COMPort(portNumber);
        }

        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            portNumber = (int)numericUpDown4.Value;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.setRates(rateMFC1, rateMFC2);
                }
                else
                {
                    MessageBox.Show("COM port connection is not open.");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            rateMFC1 = (double)numericUpDown2.Value;
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            rateMFC2 = (double)numericUpDown3.Value;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.setOdorToWaste();
                }
                else
                {
                    MessageBox.Show("COM port connection is not open");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        private void label1_Click_1(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.setOdorToUser();
                }
                else
                {
                    MessageBox.Show("COM port connection is not open");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        private void numericUpDown1_ValueChanged_1(object sender, EventArgs e)
        {
            saturationDelay = (int)numericUpDown1.Value;
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.runStep1(saturationDelay, pulseDuration);
                }
                else
                {
                    MessageBox.Show("COM port connection is not open");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            pulseDuration = (int)numericUpDown5.Value;
        }

        private void button6_Click_1(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.runStep2(saturationDelay, pulseDuration);
                }
                else
                {
                    MessageBox.Show("COM port connection is not open");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.runStep3(saturationDelay, pulseDuration);
                }
                else
                {
                    MessageBox.Show("COM port connection is not open");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.runStep4(saturationDelay, pulseDuration);
                }
                else
                {
                    MessageBox.Show("COM port connection is not open");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.runStep5(saturationDelay, pulseDuration);
                }
                else
                {
                    MessageBox.Show("COM port connection is not open");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.setOdorSourceOn();
                }
                else
                {
                    MessageBox.Show("COM port connection is not open");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            if (port != null)
            {
                if (port.getConnectionActive() == true)
                {
                    port.setOdorSourceOff();
                }
                else
                {
                    MessageBox.Show("COM port connection is not open");
                }
            }
            else
            {
                MessageBox.Show("COM port connection is not open");
            }
        }
    }
}
