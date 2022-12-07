using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SerialSendImage
{
    public partial class MainForm : Form
    {
        private SerialPort serialPort = null;
        private Thread sendingThread = null;
        private bool sending = false;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void OnSerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                Thread.Sleep(250);
                byte[] buffer = new byte[20];
                int n = this.serialPort.Read(buffer, 0, buffer.Length);
                if (n > 0)
                {
                    Debug.WriteLine("Read " + n + " bytes from serial port.");
                    StringBuilder sb = new StringBuilder(40);
                    sb.Append("Received:");
                    for (int i = 0; i < n; i++)
                    {
                        sb.Append(" ").Append(buffer[i].ToString("X2"));
                    }
                    string s = sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        private void ToggleSerialPort(object sender, EventArgs e)
        {
            if (this.serialPort == null)
            {
                ConfigForm config = new ConfigForm();
                config.ShowDialog();
                if (config.serialPort != null)
                {
                    OpenPort(config.serialPort);
                }
            }
            else
            {
                ClosePort();
            }
        }
        private void OpenPort(SerialPort port)
        {
            this.serialPort = port;
            this.serialPort.DataReceived += this.OnSerialPortDataReceived;
            this.btnSend.Enabled = true;
            this.btnOpen.Image = global::SerialSendImage.Properties.Resources.stop;
            this.btnOpen.Text = "Close Serial Port";
            SetStatus("Serial port is open.");
        }

        private void ClosePort()
        {
            if (this.serialPort != null)
            { 
                this.serialPort.DataReceived -= this.OnSerialPortDataReceived;
                this.serialPort.Close();
                this.serialPort = null;
                this.btnSend.Enabled = false;
                this.btnOpen.Image = global::SerialSendImage.Properties.Resources.start;
                this.btnOpen.Text = "Open Serial Port";
                SetStatus("Serial port is closed.");
            }
        }

        private void SetStatus(string text)
        {
            this.statusStripLabel.Text = text;
        }

        private void OpenFile(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog()==DialogResult.OK && dialog.FileName!="") {
                Image image = Image.FromFile(dialog.FileName);
                picSource.Image = Stetch(image);
                Debug.WriteLine("image size: " + picSource.Image.Width);
            }
            dialog.Dispose();
        }

        private Image Stetch(Image image)
        {
            if (image.Width == 640 && image.Height == 480)
            {
                return image;
            }
            return image.GetThumbnailImage(640, 480, () => { return false;  }, IntPtr.Zero);
        }

        private void SendImage(object sender, EventArgs e)
        {
            if(this.btnSend.Text == "Cancel")
            {
                EndSending();
            }
            else
            {
                this.pbSend.Value = 0;
                this.btnSend.Text = "Cancel";
                this.btnOpen.Enabled = false;
                this.btnChoose.Enabled = false;
                Image bitmap = this.picSource.Image.Clone() as Image;
                this.sendingThread = new Thread(() => {
                    SendImageData(bitmap);
                });
                this.sendingThread.Start();
            }
        }

        private void SendImageData(Image img)
        {
            Bitmap bitmap = img.Clone() as Bitmap;
            if (bitmap==null || bitmap.Width!=640 || bitmap.Height != 480)
            {
                Debug.WriteLine("Invalid bitmap.");
                return;
            }
            Debug.WriteLine("Serial port sending thread started.");
            this.sending = true;
            // address range: [0xB80000 ~ 0xCAC000), 4 bytes per pixel
            const int ADDR_OFFSET = 0xB80000;
            int addr = ADDR_OFFSET;
            for (int y = 0; y < 480; y++)
            {
                List<byte> buffer = new List<byte>(640 * 8);
                for (int x=0; x < 640; x++)
                {
                    if (!this.sending)
                    {
                        this.Invoke(new EventHandler(delegate {
                            this.EndSending();
                        }));
                        Debug.WriteLine("Sending thread interrupted.");
                        return;
                    }
                    Color c = bitmap.GetPixel(x, y);
                    int argb = c.ToArgb();
                    // 8 bytes data format: 8'action, 24'addr, 32'argb-data:
                    buffer.Add(0x01);
                    buffer.Add((byte)((addr & 0xff0000) >> 16));
                    buffer.Add((byte)((addr & 0xff00) >> 8));
                    buffer.Add((byte)(addr & 0xff));
                    // ARGB: A=0
                    buffer.Add(0);
                    buffer.Add((byte)((argb & 0xff0000) >> 16));
                    buffer.Add((byte)((argb & 0xff00) >> 8));
                    buffer.Add((byte)(argb & 0xff));
                    addr += 4;
                }
                try
                {
                    this.serialPort.Write(buffer.ToArray(), 0, buffer.Count);
                    Thread.Sleep(5);
                }
                catch (Exception ex)
                {
                    ClosePort();
                    Debug.WriteLine(ex.ToString());
                    return;
                }
                Debug.WriteLine((y + 1) + " / 480 lines sent.");
                this.Invoke(new EventHandler(delegate {
                    if (y % 2 == 1)
                    {
                        Bitmap copy = new Bitmap(640, 480);
                        Graphics g = Graphics.FromImage(copy);
                        g.DrawImageUnscaledAndClipped(bitmap, new Rectangle(0, 0, 640, y + 1));
                        g.Dispose();
                        this.picTarget.Image = copy;
                        // bitmap.Clone(new Rectangle(0, 0, 640, y + 1), bitmap.PixelFormat);
                        
                        //Image image = Image.;
                    }
                    SetStatus((640 * (y+1)) + " / " + (640 * 480) + " pixels sent.");
                    this.pbSend.Value = this.pbSend.Value + 1;
                }));
            }
            this.Invoke(new EventHandler(delegate {
                this.EndSending();
            }));
            Debug.WriteLine("Serial port sending thread ended.");
        }

        void EndSending()
        {
            this.sending = false;
            this.btnSend.Text = "Send";
            this.btnOpen.Enabled = true;
            this.btnChoose.Enabled = true;
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (this.sending)
            {
                this.sending = false;
            }
            if (this.sendingThread != null)
            {
                this.sendingThread.Join();
            }
            ClosePort();
        }
    }
}
