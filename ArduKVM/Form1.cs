using ArduKVM.Properties;
using DDCCI;
using SharpHook;
using SharpHook.Native;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Runtime.InteropServices;
namespace ArduKVM
{
    public partial class Form1 : Form
    {

        private SerialPort serialPort = new SerialPort();

        private SimpleGlobalHook globalHook = new();
        private bool isControllingPCB = false;

        byte[] keyboardReport = new byte[9];
        byte[] mouseReport = new byte[5];

        short currentX, currentY = 0;
        bool stop = true;

        static DisplayService displayService = new DisplayService();
        private MonitorInfo monitor;

        uint currentInput = 0;
        uint maxInputValue = 0;

        byte INPUT_HDMI2 = 0x12;
        byte INPUT_DP = 0x0f;

        byte wheel;

        short centerX = (short)(Screen.PrimaryScreen.Bounds.Width / 2);
        short centerY = (short)(Screen.PrimaryScreen.Bounds.Height / 2);


        BlockingCollection<byte[]> queue = new BlockingCollection<byte[]>(100);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            var monitors = displayService.GetMonitors();
            monitor = monitors.First();
            displayService.GetCapabilities(monitor);
            dxva2.GetVCPFeatureAndVCPFeatureReply(monitor.Handle, (char)0x60, IntPtr.Zero, out currentInput, out maxInputValue);

            keyboardReport[0] = 0xb3;
            mouseReport[0] = 0xb4;

            globalHook.KeyReleased += OnKeyReleased;
            globalHook.KeyPressed += OnKeyPressed;
            globalHook.MouseMoved += OnMouseMoved;
            globalHook.MousePressed += OnMousePressed;
            globalHook.MouseReleased += OnMouseReleased;
            globalHook.MouseWheel += OnMouseWheel;
            globalHook.MouseDragged += OnMouseMoved;

            serialPort.ErrorReceived += OnPortDisconnected;

            foreach (var port in SerialPort.GetPortNames())
            {
                cbPorts.Items.Add(port);
            }

            if (cbPorts.Items.Count == 0)
            {
                MessageBox.Show("No serial ports found");
                Application.Exit();
            }

            cbPorts.SelectedIndex = 0;


            if (Settings.Default.port != "")
            {
                if (Connect(Settings.Default.port))
                {
                    notifyIcon.Visible = true;
                    notifyIcon.ShowBalloonTip(1, "ArduKVM", "Connected to " + Settings.Default.port, ToolTipIcon.Info);
                }
            }
            workerSerial.RunWorkerAsync();
            globalHook.Run();

        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {

            var key = MapKeyCode(e.Data.KeyCode);
            var modifier = MapKeyModifier(e.Data.KeyCode);

            if (modifier != 0)
            {
                if ((keyboardReport[1] & modifier) != modifier)
                {
                    keyboardReport[1] |= modifier;
                    SendReport(true);
                }
                return;
            }

            if (!isControllingPCB)
            {
                return;
            }
            e.SuppressEvent = true;

            int zeroIdx = 0;

            for (int i = 3; i < 9; i++)
            {
                if (keyboardReport[i] == key)
                {
                    return;
                }
                if (zeroIdx == 0 && keyboardReport[i] == 0)
                {
                    zeroIdx = i;
                }
            }
            if (zeroIdx == 0)
            {
                return;
            }
            keyboardReport[zeroIdx] = key;
            SendReport(true);
        }

        private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
        {

            if (e.Data.KeyCode == KeyCode.VcInsert)
            {
                if (keyboardReport[1] == 0x05)
                {
                    SwitchPCs();
                    return;
                }
            }

            if (!isControllingPCB)
            {
                return;
            }

            e.SuppressEvent = true;

            var key = MapKeyCode(e.Data.KeyCode);
            var modifier = MapKeyModifier(e.Data.KeyCode);
            if (modifier != 0 && ((keyboardReport[1] & modifier) == modifier))
            {
                keyboardReport[1] &= (byte)~modifier;
                SendReport(true);
                return;
            }

            for (int i = 3; i < 9; i++)
            {
                if (keyboardReport[i] == key)
                {
                    keyboardReport[i] = 0;
                }
            }

            SendReport(true);
        }

        private void OnMouseMoved(object? sender, MouseHookEventArgs e)
        {
            if (!isControllingPCB)
            {
                return;
            }

            e.SuppressEvent = true;

            currentX = e.Data.X;
            currentY = e.Data.Y;
        }

        private void OnMousePressed(object? sender, MouseHookEventArgs e)
        {

            if (!isControllingPCB)
            {
                return;
            }
            e.SuppressEvent = true;

            switch (e.Data.Button)
            {
                case MouseButton.Button1:
                    mouseReport[1] |= 1;
                    break;
                case MouseButton.Button2:
                    mouseReport[1] |= 2;
                    break;
                case MouseButton.Button3:
                    mouseReport[1] |= 4;
                    break;
            }
            mouseReport[2] = 0;
            mouseReport[3] = 0;
            SendReport(false);
        }
        private void OnMouseReleased(object? sender, MouseHookEventArgs e)
        {

            if (!isControllingPCB)
            {
                return;
            }
            e.SuppressEvent = true;

            switch (e.Data.Button)
            {
                case MouseButton.Button1:
                    mouseReport[1] &= unchecked((byte)~1);
                    break;
                case MouseButton.Button2:
                    mouseReport[1] &= unchecked((byte)~2);
                    break;
                case MouseButton.Button3:
                    mouseReport[1] &= unchecked((byte)~4);
                    break;
            }
            mouseReport[2] = 0;
            mouseReport[3] = 0;
            SendReport(false);
        }


        private void OnMouseWheel(object? sender, MouseWheelHookEventArgs e)
        {

            if (!isControllingPCB)
            {
                return;
            }
            e.SuppressEvent = true;

            wheel = e.Data.Rotation > 128 ? (byte)1 : unchecked((byte)-1);
        }


        private void btnApply_Click(object sender, EventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }

            if (Connect(cbPorts.SelectedItem.ToString()))
            {
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(1000, "ArduKVM", "Connected to " + cbPorts.SelectedItem.ToString(), ToolTipIcon.Info);

                this.Hide();
                Settings.Default.port = cbPorts.SelectedItem.ToString();
                Settings.Default.Save();
                return;
            }
        }

        private bool Connect(string port)
        {

            serialPort.PortName = port;
            serialPort.BaudRate = 1228800;
            try
            {
                serialPort.Open();
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to open ${port}," + e.Message);
                return false;
            }

            return true;
        }
        private void OnPortDisconnected(object sender, SerialErrorReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            globalHook.Dispose();
        }

        private void timerPps_Tick(object sender, EventArgs e)
        {
            if (wheel != 0)
            {
                if (mouseReport[4] != 0)
                {
                    mouseReport[4] = 0;
                    SendReport(false);
                    wheel = 0;
                }
                else
                {
                    mouseReport[4] = wheel;
                    SendReport(false);
                }

            }

            if (currentX == centerX && currentY == centerY)
            {
                if (!stop)
                {
                    mouseReport[2] = 0;
                    mouseReport[3] = 0;
                    SendReport(false);
                }
                stop = true;
                return;
            }
            stop = false;

            var deltaX = (3 * (currentX - centerX));
            var deltaY = (3 * (currentY - centerY));

            mouseReport[2] = deltaX > 127 ? (byte)127 : (deltaX < -127 ? unchecked((byte)-127) : (byte)deltaX);
            mouseReport[3] = deltaY > 127 ? (byte)127 : (deltaY < -127 ? unchecked((byte)-127) : (byte)deltaY);

            SendReport(false);

            SetCursorPos(Screen.PrimaryScreen.Bounds.Width / 2, Screen.PrimaryScreen.Bounds.Height / 2);
            currentX = centerX;
            currentY = centerY;

        }

        private void SendReport(bool keyboard)
        {
            if (!serialPort.IsOpen)
            {
                return;
            }

            if (!isControllingPCB)
            {
                return;
            }

            if (keyboard)
            {
                queue.Add(keyboardReport);
            }
            else
            {
                queue.Add(mouseReport);
            }
        }

        private void SwitchPCs()
        {
            timerInput.Stop();
            if (!isControllingPCB)
            {
                isControllingPCB = true;
                timerPps.Enabled = true;
                notifyIcon.BalloonTipText = "Controlling PC B";
                notifyIcon.ShowBalloonTip(1);
                displayService.SetVCPCapability(monitor, (char)0x60, INPUT_HDMI2);
            }
            else
            {
                Array.Clear(keyboardReport, 1, keyboardReport.Length - 1);
                Array.Clear(mouseReport, 1, mouseReport.Length - 1);
                SendReport(true);
                SendReport(false);
                isControllingPCB = false;
                timerPps.Enabled = false;
                notifyIcon.BalloonTipText = "Disabled";
                notifyIcon.ShowBalloonTip(1);
                displayService.SetVCPCapability(monitor, (char)0x60, INPUT_DP);
            }
            timerInput.Start();
        }
        public static class HIDModifiers
        {
            public const byte LEFTCTRL = 0x01;
            public const byte LEFTSHIFT = 0x02;
            public const byte LEFTALT = 0x04;
            public const byte LEFTGUI = 0x08;
            public const byte RIGHTCTRL = 0x10;
            public const byte RIGHTSHIFT = 0x20;
            public const byte RIGHTALT = 0x40;
            public const byte RIGHTGUI = 0x80;
        }

        public static byte MapKeyCode(KeyCode keyCode)
        {
            byte keycode = 0;

            switch (keyCode)
            {
                // Letters
                case KeyCode.VcA: keycode = 0x04; break;
                case KeyCode.VcB: keycode = 0x05; break;
                case KeyCode.VcC: keycode = 0x06; break;
                case KeyCode.VcD: keycode = 0x07; break;
                case KeyCode.VcE: keycode = 0x08; break;
                case KeyCode.VcF: keycode = 0x09; break;
                case KeyCode.VcG: keycode = 0x0A; break;
                case KeyCode.VcH: keycode = 0x0B; break;
                case KeyCode.VcI: keycode = 0x0C; break;
                case KeyCode.VcJ: keycode = 0x0D; break;
                case KeyCode.VcK: keycode = 0x0E; break;
                case KeyCode.VcL: keycode = 0x0F; break;
                case KeyCode.VcM: keycode = 0x10; break;
                case KeyCode.VcN: keycode = 0x11; break;
                case KeyCode.VcO: keycode = 0x12; break;
                case KeyCode.VcP: keycode = 0x13; break;
                case KeyCode.VcQ: keycode = 0x14; break;
                case KeyCode.VcR: keycode = 0x15; break;
                case KeyCode.VcS: keycode = 0x16; break;
                case KeyCode.VcT: keycode = 0x17; break;
                case KeyCode.VcU: keycode = 0x18; break;
                case KeyCode.VcV: keycode = 0x19; break;
                case KeyCode.VcW: keycode = 0x1A; break;
                case KeyCode.VcX: keycode = 0x1B; break;
                case KeyCode.VcY: keycode = 0x1C; break;
                case KeyCode.VcZ: keycode = 0x1D; break;

                // Numbers
                case KeyCode.Vc1: keycode = 0x1E; break;
                case KeyCode.Vc2: keycode = 0x1F; break;
                case KeyCode.Vc3: keycode = 0x20; break;
                case KeyCode.Vc4: keycode = 0x21; break;
                case KeyCode.Vc5: keycode = 0x22; break;
                case KeyCode.Vc6: keycode = 0x23; break;
                case KeyCode.Vc7: keycode = 0x24; break;
                case KeyCode.Vc8: keycode = 0x25; break;
                case KeyCode.Vc9: keycode = 0x26; break;
                case KeyCode.Vc0: keycode = 0x27; break;

                // Function keys
                case KeyCode.VcF1: keycode = 0x3A; break;
                case KeyCode.VcF2: keycode = 0x3B; break;
                case KeyCode.VcF3: keycode = 0x3C; break;
                case KeyCode.VcF4: keycode = 0x3D; break;
                case KeyCode.VcF5: keycode = 0x3E; break;
                case KeyCode.VcF6: keycode = 0x3F; break;
                case KeyCode.VcF7: keycode = 0x40; break;
                case KeyCode.VcF8: keycode = 0x41; break;
                case KeyCode.VcF9: keycode = 0x42; break;
                case KeyCode.VcF10: keycode = 0x43; break;
                case KeyCode.VcF11: keycode = 0x44; break;
                case KeyCode.VcF12: keycode = 0x45; break;

                // Special keys
                case KeyCode.VcEnter: keycode = 0x28; break;
                case KeyCode.VcEscape: keycode = 0x29; break;
                case KeyCode.VcBackspace: keycode = 0x2A; break;
                case KeyCode.VcTab: keycode = 0x2B; break;
                case KeyCode.VcSpace: keycode = 0x2C; break;
                case KeyCode.VcMinus: keycode = 0x2D; break;
                case KeyCode.VcEquals: keycode = 0x2E; break;
                case KeyCode.VcOpenBracket: keycode = 0x2F; break;
                case KeyCode.VcCloseBracket: keycode = 0x30; break;
                case KeyCode.VcBackslash: keycode = 0x31; break;
                case KeyCode.VcSemicolon: keycode = 0x33; break;
                case KeyCode.VcQuote: keycode = 0x34; break;
                case KeyCode.VcBackQuote: keycode = 0x35; break;
                case KeyCode.VcComma: keycode = 0x36; break;
                case KeyCode.VcPeriod: keycode = 0x37; break;
                case KeyCode.VcSlash: keycode = 0x38; break;
                case KeyCode.VcCapsLock: keycode = 0x39; break;

                // Navigation
                case KeyCode.VcInsert: keycode = 0x49; break;
                case KeyCode.VcHome: keycode = 0x4A; break;
                case KeyCode.VcPageUp: keycode = 0x4B; break;
                case KeyCode.VcDelete: keycode = 0x4C; break;
                case KeyCode.VcEnd: keycode = 0x4D; break;
                case KeyCode.VcPageDown: keycode = 0x4E; break;
                case KeyCode.VcRight: keycode = 0x4F; break;
                case KeyCode.VcLeft: keycode = 0x50; break;
                case KeyCode.VcDown: keycode = 0x51; break;
                case KeyCode.VcUp: keycode = 0x52; break;
            }

            return keycode;
        }

        public static byte MapKeyModifier(KeyCode keyCode)
        {
            byte currentModifiers = 0;

            switch (keyCode)
            {
                case KeyCode.VcLeftControl:
                    currentModifiers |= HIDModifiers.LEFTCTRL;
                    break;
                case KeyCode.VcRightControl:
                    currentModifiers |= HIDModifiers.RIGHTCTRL;
                    break;
                case KeyCode.VcLeftShift:
                    currentModifiers |= HIDModifiers.LEFTSHIFT;
                    break;
                case KeyCode.VcRightShift:
                    currentModifiers |= HIDModifiers.RIGHTSHIFT;
                    break;
                case KeyCode.VcLeftAlt:
                    currentModifiers |= HIDModifiers.LEFTALT;
                    break;
                case KeyCode.VcRightAlt:
                    currentModifiers |= HIDModifiers.RIGHTALT;
                    break;
                case KeyCode.VcLeftMeta:
                    currentModifiers |= HIDModifiers.LEFTGUI;
                    break;
                case KeyCode.VcRightMeta:
                    currentModifiers |= HIDModifiers.RIGHTGUI;
                    break;
            }

            return currentModifiers;
        }

        private void workerConnect_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            byte[] data;
            while (true)
            {
                data = queue.Take();
                serialPort.Write(data, 0, data.Length);

            }
        }

        private void timerInput_Tick(object sender, EventArgs e)
        {
            dxva2.GetVCPFeatureAndVCPFeatureReply(monitor.Handle, (char)0x60, IntPtr.Zero, out currentInput, out maxInputValue);
            if (currentInput == INPUT_HDMI2 && !isControllingPCB)
            {
                SwitchPCs();
            }
            else if (currentInput == INPUT_DP && isControllingPCB)
            {
                SwitchPCs();
            }
        }
    }
}
