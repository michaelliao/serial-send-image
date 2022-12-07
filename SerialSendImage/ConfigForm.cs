using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SerialSendImage
{
    public partial class ConfigForm : Form
    {
        public SerialPort serialPort = null;

        public ConfigForm()
        {
            InitializeComponent();
        }

        private void RefreshPorts(object sender, EventArgs e)
        {
            this.cmbPort.Items.Clear();
            foreach (string name in SerialPort.GetPortNames())
            {
                this.cmbPort.Items.Add(name);
            }
        }

        private void OnLoad(object sender, EventArgs e)
        { 
            this.cmbBaud.SelectedIndex = 0;
            this.cmbDataBits.SelectedIndex = 0;
            this.cmbParity.SelectedIndex = 0;
            this.cmbStopBits.SelectedIndex = 0;
            RefreshPorts(sender, e);
        }

        private void OpenPort(object sender, EventArgs e)
        {
            object selected = this.cmbPort.SelectedItem;
            if (selected == null)
            {
                MessageBox.Show("Please select a serial port.", "Cannot Open Port", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            {
                SerialPort port = new SerialPort();
                port.PortName = selected.ToString();
                port.BaudRate = int.Parse(this.cmbBaud.SelectedItem.ToString());
                port.DataBits = int.Parse(this.cmbDataBits.SelectedItem.ToString());
                port.Parity = (Parity)Enum.Parse(typeof(Parity), this.cmbParity.SelectedItem.ToString());
                port.StopBits = (StopBits)Enum.Parse(typeof(StopBits), this.cmbStopBits.SelectedItem.ToString());
                port.DtrEnable = true;
                port.RtsEnable = true;
                port.Handshake = Handshake.None;
                try
                {
                    port.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Cannot Open Port", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                if (port.IsOpen)
                {
                    this.serialPort = port;
                    this.Close();
                }
            }
        }
    }
}
