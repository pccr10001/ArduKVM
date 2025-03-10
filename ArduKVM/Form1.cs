using ArduKVM.Properties;
using DDCCI;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace ArduKVM
{
    public partial class Form1 : Form
    {

        private SerialPort serialPort = new SerialPort();

        private bool isControllingExternalPC = false;

        byte[] keyboardReport = new byte[9];
        byte[] mouseReport = new byte[5];

        short currentX, currentY = 0;
        bool stop = true;
        bool toSwitchPC = false;
        bool initialized = false;

        int portCount = 0;
        int selectedInput = -1;
        int currentInput = 0;
        string hostInput = "";

        static DisplayService displayService = new DisplayService();
        static INodeFormatter nodeFormatter = new NodeFormatter();
        static ITokenizer tokenizer = new CapabilitiesTokenizer();
        static IParser parser = new CapabilitiesParser();

        private MonitorInfo monitor;

        uint maxInputValue = 0;

        byte wheel;

        IntPtr mouseContext;
        IntPtr keyboardContext;

        byte mouseButtonState = 0;

        bool pausePressed = false;

        BlockingCollection<byte[]> queue = new BlockingCollection<byte[]>(100);

        private List<ComboBox> cbInputs = new List<ComboBox>();
        private List<ComboBox> cbPorts = new List<ComboBox>();
        private Dictionary<string, string> inputSources = new Dictionary<string, string>();
        List<PortMapping> mappings = new List<PortMapping>();


        private const int INTERCEPTION_FILTER_MOUSE_ALL = 0xFFFF;
        private const int INTERCEPTION_FILTER_KEY_ALL = 0xFFFF;

        private const int INTERCEPTION_KEY_DOWN = 0x00;
        private const int INTERCEPTION_KEY_UP = 0x01;
        private const int INTERCEPTION_KEY_E0 = 0x02;
        private const int INTERCEPTION_KEY_E1 = 0x04;


        private const int INTERCEPTION_MOUSE_LEFT_BUTTON_DOWN = 0x001;
        private const int INTERCEPTION_MOUSE_LEFT_BUTTON_UP = 0x002;
        private const int INTERCEPTION_MOUSE_RIGHT_BUTTON_DOWN = 0x004;
        private const int INTERCEPTION_MOUSE_RIGHT_BUTTON_UP = 0x008;
        private const int INTERCEPTION_MOUSE_MIDDLE_BUTTON_DOWN = 0x010;
        private const int INTERCEPTION_MOUSE_MIDDLE_BUTTON_UP = 0x020;

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseStroke
        {
            public short state;
            public short flags;
            public short rolling;
            public int x;
            public int y;
            public uint information;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyStroke
        {
            public short code;
            public short state;
            public uint information;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InterceptionStroke
        {
            [FieldOffset(0)]
            public MouseStroke Mouse;

            [FieldOffset(0)]
            public KeyStroke Keyboard;
        }


        class PortMapping
        {
            public string Port { get; set; }
            public string Input { get; set; }
        }

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr interception_create_context();

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void interception_destroy_context(IntPtr context);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int interception_receive(IntPtr context, int device, ref InterceptionStroke stroke, uint nstroke);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int interception_send(IntPtr context, int device, ref InterceptionStroke stroke, uint nstroke);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void interception_set_filter(IntPtr context, Predicate predicate, int filter);

        private delegate int Predicate(int device);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool interception_is_mouse(int device);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool interception_is_keyboard(int device);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int interception_wait_with_timeout(IntPtr context, UInt32 milliseconds);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int interception_wait(IntPtr context);

        public Form1()
        {
            InitializeComponent();
        }

        private bool InitializeDisplay()
        {
            try
            {
                var cs = displayService.GetCapabilities(monitor);
                Console.WriteLine($"CapabilitiesString: {cs}");

                var tokens = tokenizer.GetTokens(cs);
                var node = parser.Parse(tokens);

                var vcpNode = node.Nodes.RecursiveSelect(n => n.Nodes)
                    .Single(n => n.Value == "vcp");

                if (vcpNode == null || vcpNode.Nodes.Count() == 0)
                {
                    MessageBox.Show($"Failed to get display info, VCP info not found.");
                    return false;
                }

                var inputNode = vcpNode.Nodes.Single(n => n.Value == "60");
                if (inputNode == null || inputNode.Nodes.Count() == 0)
                {
                    MessageBox.Show($"Failed to get display info, input info not found.");
                    return false;
                }

                foreach (var capabilityNode in inputNode.Nodes.Where(c => c.Nodes == null))
                {
                    var inputSource = NodeFormatter.FormatVCPInputSource(capabilityNode.Value.ToLower());
                    if (inputSource != null)
                    {
                        inputSources.Add(inputSource, capabilityNode.Value);
                    }
                }

                if (inputSources.Count == 0)
                {
                    MessageBox.Show($"Failed to get display info, no input sources detected.");
                    return false;
                }

                return true;

            }
            catch (Exception e)
            {
                MessageBox.Show($"Failed to get display info,\n{e.Message}");
                return false;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            var monitors = displayService.GetMonitors();
            monitor = monitors.First();
            displayService.GetCapabilities(monitor);

            keyboardReport[0] = 0x73;
            mouseReport[0] = 0x74;

            cbPorts.Add(cbPort1);
            cbPorts.Add(cbPort2);
            cbPorts.Add(cbPort3);
            cbPorts.Add(cbPort4);

            cbInputs.Add(cbInput1);
            cbInputs.Add(cbInput2);
            cbInputs.Add(cbInput3);
            cbInputs.Add(cbInput4);


            for (int i = 0; i < 4; i++)
            {
                cbPorts[i].Items.Add("No used");
                cbPorts[i].SelectedIndex = 0;
            }

            foreach (var port in SerialPort.GetPortNames())
            {
                for (int i = 0; i < 4; i++)
                {
                    cbPorts[i].Items.Add(port);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                cbPorts[i].Items.Add("This PC");
            }

            if (cbPort1.Items.Count == 2)
            {
                MessageBox.Show("No serial ports found");
                Application.Exit();
                return;
            }

            if (!InitializeDisplay())
            {
                Application.Exit();
                return;
            }

            for (int i = 0; i < cbInputs.Count; i++)
            {
                for (int j = 0; j < inputSources.Keys.Count; j++)
                {
                    cbInputs[i].Items.Add(inputSources.Keys.ElementAt(j));
                }
            }

            portCount = inputSources.Count < SerialPort.GetPortNames().Length ? inputSources.Count : SerialPort.GetPortNames().Length;

            for (int i = 0; i < portCount; i++)
            {
                cbInputs[i].Enabled = true;
                cbPorts[i].Enabled = true;
                cbInputs[i].SelectedIndex = i;
            }

            try
            {
                mappings = JsonSerializer.Deserialize<List<PortMapping>>(Settings.Default.mappings);
                foreach (var mapping in mappings)
                {
                    cbPorts[mappings.IndexOf(mapping)].SelectedItem = mapping.Port;
                    cbInputs[mappings.IndexOf(mapping)].SelectedItem = inputSources.FirstOrDefault(x => x.Value == mapping.Input).Key;
                }
                notifyIcon.Visible = true;
                notifyIcon.BalloonTipText = "ArduKVM initialized.";
                notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                notifyIcon.ShowBalloonTip(1);

            }
            catch (Exception ex)
            {

            }

            UpdateCurrentInput();
            SwitchPCs();

            workerSerial.RunWorkerAsync();
            workerKeyboard.RunWorkerAsync();
        }

        private bool OnKeyPressed(short scanCode, short state)
        {

            var key = MapKeyCode(scanCode, state);
            var modifier = MapKeyModifier(scanCode, state);

            if (modifier != 0)
            {
                if ((keyboardReport[1] & modifier) != modifier)
                {
                    keyboardReport[1] |= modifier;
                    SendReport(true);
                }
                if (isControllingExternalPC)
                {
                    return false;
                }
                return true;
            }

            if (!isControllingExternalPC)
            {
                return true;
            }

            if (key == 0x48 && keyboardReport[1] == 0)
            {
                pausePressed = true;
            }

            if (key == 0x53 && keyboardReport[1] == 0 && pausePressed)
            {
                if (isControllingExternalPC)
                {
                    return false;
                }
                return true;
                
            }


            int zeroIdx = 0;

            for (int i = 3; i < 9; i++)
            {
                if (keyboardReport[i] == key)
                {
                    return false;
                }
                if (zeroIdx == 0 && keyboardReport[i] == 0)
                {
                    zeroIdx = i;
                }
            }
            if (zeroIdx == 0)
            {
                return false;
            }
            keyboardReport[zeroIdx] = key;
            SendReport(true);
            return false;
        }

        private bool OnKeyReleased(short scanCode, short state)
        {

            var key = MapKeyCode(scanCode, state);
            var modifier = MapKeyModifier(scanCode, state);

            if (key == 0 && modifier == 0)
            {
                return true;
            }

            if (scanCode == 0x52 && keyboardReport[1] == 0x05)
            {
                toSwitchPC = true;
                if (isControllingExternalPC)
                {
                    return false;
                }
                else
                {
                    return true;
                }

            }

            if (modifier != 0)
            {
                if (((keyboardReport[1] & modifier) == modifier))
                {
                    keyboardReport[1] &= (byte)~modifier;
                }

                if (toSwitchPC && keyboardReport[1] == 0)
                {
                    toSwitchPC = false;
                    Invoke((MethodInvoker)delegate { workerSwitchPC.RunWorkerAsync(); });
                }

                SendReport(true);
                return true;
            }

            toSwitchPC = false;

            if (!isControllingExternalPC)
            {
                return true;
            }

            if (key == 0x48 && keyboardReport[1] == 0)
            {
                pausePressed = true;
            }

            if (key == 0x53 && keyboardReport[1] == 0 && pausePressed)
            {
                pausePressed = false;
                if (isControllingExternalPC)
                {
                    return false;
                }
                return true;
            }

            for (int i = 3; i < 9; i++)
            {
                if (keyboardReport[i] == key)
                {
                    keyboardReport[i] = 0;
                }
            }

            SendReport(true);
            if (isControllingExternalPC)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void btnApply_Click(object sender, EventArgs e)
        {

            string hi = "";
            var m = new List<PortMapping>();
            var checkDuplicate = new List<string>();
            for (int i = 0; i < portCount; i++)
            {
                var pm = new PortMapping()
                {
                    Port = cbPorts[i].SelectedItem.ToString(),
                    Input = inputSources[cbInputs[i].SelectedItem.ToString()]
                };
                if (pm.Port == "This PC")
                {
                    if (hi != "")
                    {
                        MessageBox.Show("Only one port can be set to `This PC`");
                        return;
                    }
                    hi = cbInputs[i].SelectedItem.ToString();
                }
                if (pm.Port != "No used")
                {
                    if (checkDuplicate.Contains(pm.Port) || checkDuplicate.Contains(pm.Input))
                    {
                        MessageBox.Show("Duplicate port or input detected");
                        return;
                    }
                    checkDuplicate.Add(pm.Input);
                    checkDuplicate.Add(pm.Port);
                }

                m.Add(pm);
            }
            if (hi == "")
            {
                MessageBox.Show("One port must be set to `This PC`");
                return;
            }

            mappings.Clear();
            mappings.AddRange(m);

            hostInput = hi;
            Settings.Default.mappings = JsonSerializer.Serialize(mappings);
            Settings.Default.Save();

        }

        private bool Connect(string port)
        {

            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }

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
        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {

            this.Show();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                interception_destroy_context(mouseContext);
                interception_destroy_context(keyboardContext);
            }
            catch (Exception ex)
            {
            }

        }

        private void timerPps_Tick(object sender, EventArgs e)
        {
            if (mouseButtonState != mouseReport[1])
            {
                mouseButtonState = mouseReport[1];
                SendReport(false);
                return;
            }

            if (wheel != 0)
            {
                mouseReport[4] = wheel;
                SendReport(false);
                wheel = 0;
                return;
            }

            if (mouseReport[4] != 0)
            {
                mouseReport[4] = 0;
                SendReport(false);
                return;
            }

            if (currentX != 0 || currentY != 0)
            {
                stop = false;
                mouseReport[2] = (byte)currentX;
                mouseReport[3] = (byte)currentY;
                SendReport(false);

                currentX = 0;
                currentY = 0;
                return;
            }

            if (!stop)
            {
                mouseReport[2] = 0;
                mouseReport[3] = 0;
                SendReport(false);
                stop = true;
            }

        }

        private void SendReport(bool keyboard)
        {
            if (!serialPort.IsOpen)
            {
                return;
            }

            if (!isControllingExternalPC)
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

            UpdateCurrentInput();

            Array.Clear(keyboardReport, 1, keyboardReport.Length - 1);
            Array.Clear(mouseReport, 1, mouseReport.Length - 1);
            SendReport(true);
            SendReport(false);

            bool switchControlOnly = false;

            if (selectedInput != currentInput)
            {
                selectedInput = currentInput;
                switchControlOnly = true;
            }
            else
            {
                while (true)
                {
                    selectedInput++;
                    if (selectedInput >= portCount)
                    {
                        selectedInput = 0;
                    }
                    if (mappings[selectedInput].Port == "No used")
                    {
                        continue;
                    }
                    break;
                }
            }


            var pm = mappings[selectedInput];
            uint inputId = Convert.ToUInt32(pm.Input, 16);

            Debug.WriteLine($"Start switching");

            if (pm.Port == "This PC")
            {
                isControllingExternalPC = false;

                timerPps.Stop();
                notifyIcon.BalloonTipText = "Disabled";
                notifyIcon.ShowBalloonTip(1);
                serialPort.Close();

            }
            else
            {
                Connect(pm.Port);
                isControllingExternalPC = true;
                timerPps.Start();
                notifyIcon.BalloonTipText = "Controlling PC";
                notifyIcon.ShowBalloonTip(1);
                workerMouse.RunWorkerAsync();
            }

            if (!switchControlOnly)
            {
                displayService.SetVCPCapability(monitor, (char)0x60, (int)inputId);
            }
            UpdateCurrentInput();

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

        private byte MapKeyCode(short scanCode, short state)
        {
            bool isE0 = (state & INTERCEPTION_KEY_E0) != 0;
            bool isE1 = (state & INTERCEPTION_KEY_E1) != 0;

            int hidCode = 0;

            if (isE0)
            {
                switch (scanCode)
                {
                    case 0x52: hidCode = 0x49; break; // Insert/Numpad 0
                    case 0x47: hidCode = 0x4A; break; // Home/Numpad 7
                    case 0x49: hidCode = 0x4B; break; // Page Up/Numpad 8
                    case 0x53: hidCode = 0x4C; break; // Delete/Numpad .
                    case 0x4F: hidCode = 0x4D; break; // End/Numpad 1
                    case 0x51: hidCode = 0x4E; break; // Page Down/Numpad 2
                    case 0x4D: hidCode = 0x4F; break; // Right/Numpad 6
                    case 0x4B: hidCode = 0x50; break; // Left/Numpad 4
                    case 0x50: hidCode = 0x51; break; // Down/Numpad 5
                    case 0x48: hidCode = 0x52; break; // Up/Numpad 8
                    case 0x1C: hidCode = 0x58; break; // Enter
                    case 0x35: hidCode = 0x54; break; // /
                    case 0x37: hidCode = 0x46; break; // PrintScreen

                    default:
                        break;
                }
                return (byte)hidCode;
            }

            switch (scanCode)
            {

                case 0x1E: hidCode = 0x04; break; // A
                case 0x30: hidCode = 0x05; break; // B
                case 0x2E: hidCode = 0x06; break; // C
                case 0x20: hidCode = 0x07; break; // D
                case 0x12: hidCode = 0x08; break; // E
                case 0x21: hidCode = 0x09; break; // F
                case 0x22: hidCode = 0x0A; break; // G
                case 0x23: hidCode = 0x0B; break; // H
                case 0x17: hidCode = 0x0C; break; // I
                case 0x24: hidCode = 0x0D; break; // J
                case 0x25: hidCode = 0x0E; break; // K
                case 0x26: hidCode = 0x0F; break; // L
                case 0x32: hidCode = 0x10; break; // M
                case 0x31: hidCode = 0x11; break; // N
                case 0x18: hidCode = 0x12; break; // O
                case 0x19: hidCode = 0x13; break; // P
                case 0x10: hidCode = 0x14; break; // Q
                case 0x13: hidCode = 0x15; break; // R
                case 0x1F: hidCode = 0x16; break; // S
                case 0x14: hidCode = 0x17; break; // T
                case 0x16: hidCode = 0x18; break; // U
                case 0x2F: hidCode = 0x19; break; // V
                case 0x11: hidCode = 0x1A; break; // W
                case 0x2D: hidCode = 0x1B; break; // X
                case 0x15: hidCode = 0x1C; break; // Y
                case 0x2C: hidCode = 0x1D; break; // Z

                case 0x02: hidCode = 0x1E; break; // 1
                case 0x03: hidCode = 0x1F; break; // 2
                case 0x04: hidCode = 0x20; break; // 3
                case 0x05: hidCode = 0x21; break; // 4
                case 0x06: hidCode = 0x22; break; // 5
                case 0x07: hidCode = 0x23; break; // 6
                case 0x08: hidCode = 0x24; break; // 7
                case 0x09: hidCode = 0x25; break; // 8
                case 0x0A: hidCode = 0x26; break; // 9
                case 0x0B: hidCode = 0x27; break; // 0

                case 0x3B: hidCode = 0x3A; break; // F1
                case 0x3C: hidCode = 0x3B; break; // F2
                case 0x3D: hidCode = 0x3C; break; // F3
                case 0x3E: hidCode = 0x3D; break; // F4
                case 0x3F: hidCode = 0x3E; break; // F5
                case 0x40: hidCode = 0x3F; break; // F6
                case 0x41: hidCode = 0x40; break; // F7
                case 0x42: hidCode = 0x41; break; // F8
                case 0x43: hidCode = 0x42; break; // F9
                case 0x44: hidCode = 0x43; break; // F10
                case 0x57: hidCode = 0x44; break; // F11
                case 0x58: hidCode = 0x45; break; // F12

                case 0x01: hidCode = 0x29; break; // ESC
                case 0x0E: hidCode = 0x2A; break; // Backspace
                case 0x0F: hidCode = 0x2B; break; // Tab

                case 0x39: hidCode = 0x2C; break; // Space
                case 0x0C: hidCode = 0x2D; break; // -
                case 0x0D: hidCode = 0x2E; break; // =
                case 0x1A: hidCode = 0x2F; break; // [
                case 0x1B: hidCode = 0x30; break; // ]
                case 0x2B: hidCode = 0x31; break; // \
                case 0x27: hidCode = 0x33; break; // ;
                case 0x28: hidCode = 0x34; break; // '
                case 0x29: hidCode = 0x35; break; // `
                case 0x33: hidCode = 0x36; break; // ,
                case 0x34: hidCode = 0x37; break; // .
                case 0x35: hidCode = 0x38; break; // / 
                case 0x3A: hidCode = 0x39; break; // Caps Lock

                case 0x52: hidCode = 0x62; break; // Numpad 0
                case 0x47: hidCode = 0x5F; break; // Numpad 7
                case 0x48: hidCode = 0x60; break; // Numpad 8
                case 0x53: hidCode = 0x63; break; // Numpad .
                case 0x4F: hidCode = 0x59; break; // Numpad 1
                case 0x50: hidCode = 0x5A; break; // Numpad 2
                case 0x51: hidCode = 0x5B; break; // Numpad 3
                case 0x4D: hidCode = 0x5E; break; // Numpad 6
                case 0x4B: hidCode = 0x5C; break; // Numpad 4
                case 0x4C: hidCode = 0x5D; break; // Numpad 5
                case 0x49: hidCode = 0x61; break; // Numpad 9
                case 0x1C: hidCode = 0x28; break; // Numpad Enter 
                case 0x45: hidCode = 0x53; break; // Num Lock
                case 0x37: hidCode = 0x55; break; // Numpad *
                case 0x4A: hidCode = 0x56; break; // Numpad -
                case 0x4E: hidCode = 0x57; break; // Numpad +
                case 0x1D: hidCode = isE1 ? 0x48 : 0x00; break; // Pause
                case 0x46: hidCode = 0x47; break; // Scroll Lock

            }

            return (byte)hidCode;
        }

        private byte MapKeyModifier(short scanCode, short state)
        {
            bool isE0 = (state & INTERCEPTION_KEY_E0) != 0;
            bool isE1 = (state & INTERCEPTION_KEY_E1) != 0;

            byte modifier = 0;

            switch (scanCode)
            {
                case 0x1D: // Ctrl
                    if (isE1)
                    {
                        break;
                    }
                    modifier = isE0 ? HIDModifiers.RIGHTCTRL : HIDModifiers.LEFTCTRL;
                    break;
                case 0x2A: // Left Shift
                    modifier = isE0 ? (byte)0 : HIDModifiers.LEFTSHIFT;
                    break;
                case 0x36: // Right Shift
                    modifier = HIDModifiers.RIGHTSHIFT;
                    break;
                case 0x38: // Alt
                    modifier = isE0 ? HIDModifiers.RIGHTALT : HIDModifiers.LEFTALT;
                    break;
                case 0x5B: // Left Win (E0)
                    if (isE0) modifier = HIDModifiers.LEFTGUI;
                    break;
                case 0x5C: // Right Win (E0)
                    if (isE0) modifier = HIDModifiers.RIGHTGUI;
                    break;
            }

            return modifier;
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


        private void UpdateCurrentInput()
        {
            uint current;
            dxva2.GetVCPFeatureAndVCPFeatureReply(monitor.Handle, (char)0x60, IntPtr.Zero, out current, out maxInputValue);
            for (int i = 0; i < mappings.Count; i++)
            {
                if (current == Convert.ToInt32(mappings[i].Input, 16))
                {
                    currentInput = i;
                    break;
                }
            }
        }

        private void workerMouse_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            mouseContext = interception_create_context();

            interception_set_filter(mouseContext,
                device => interception_is_mouse(device) ? 1 : 0,
                INTERCEPTION_FILTER_MOUSE_ALL);

            int device = 0;
            int mouse = 0;

            InterceptionStroke stroke = new InterceptionStroke();
            while (isControllingExternalPC)
            {
                if (mouse == 0)
                {
                    device = interception_wait_with_timeout(mouseContext, 1000);
                    if (device == 0)
                    {
                        continue;
                    }
                    if (interception_is_mouse(device))
                    {
                        mouse = device;
                    }
                }


                if (interception_wait_with_timeout(mouseContext, 1000) != mouse)
                {
                    continue;
                }

                if (interception_receive(mouseContext, mouse, ref stroke, 1) > 0)
                {
                    switch (stroke.Mouse.state)
                    {
                        case INTERCEPTION_MOUSE_LEFT_BUTTON_DOWN: mouseReport[1] |= 1; break;
                        case INTERCEPTION_MOUSE_LEFT_BUTTON_UP: mouseReport[1] &= unchecked((byte)~1); break;
                        case INTERCEPTION_MOUSE_RIGHT_BUTTON_DOWN: mouseReport[1] |= 2; break;
                        case INTERCEPTION_MOUSE_RIGHT_BUTTON_UP: mouseReport[1] &= unchecked((byte)~2); break;
                        case INTERCEPTION_MOUSE_MIDDLE_BUTTON_DOWN: mouseReport[1] |= 4; break;
                        case INTERCEPTION_MOUSE_MIDDLE_BUTTON_UP: mouseReport[1] &= unchecked((byte)~4); break;

                        default:

                            if (stroke.Mouse.rolling != 0)
                            {
                                wheel = (byte)stroke.Mouse.rolling;
                                break;
                            }
                            currentX += (short)stroke.Mouse.x;
                            currentY += (short)stroke.Mouse.y;
                            break;
                    }

                }
            }
            interception_destroy_context(mouseContext);
            Debug.WriteLine("WorkerMouse done");
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (!initialized && mappings.Count > 0)
            {
                this.Hide();
                initialized = true;
            }
        }

        private void workerKeyboard_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            keyboardContext = interception_create_context();

            interception_set_filter(keyboardContext,
                device => interception_is_keyboard(device) ? 1 : 0,
                INTERCEPTION_FILTER_KEY_ALL);

            int device = 0;
            int keyboard = 0;

            InterceptionStroke stroke = new InterceptionStroke();

            while (true)
            {
                if (keyboard == 0)
                {
                    device = interception_wait_with_timeout(keyboardContext, 1000);
                    if (device == 0)
                    {
                        continue;
                    }
                    if (interception_is_keyboard(device))
                    {
                        keyboard = device;
                    }
                }

                if (interception_wait_with_timeout(keyboardContext, 1000) != keyboard)
                {
                    continue;
                }

                if (interception_receive(keyboardContext, keyboard, ref stroke, 1) <= 0)
                {
                    continue;
                }
                bool passKey = false;
                if ((stroke.Keyboard.state & 0x01) == INTERCEPTION_KEY_DOWN)
                {
                    passKey = OnKeyPressed(stroke.Keyboard.code, stroke.Keyboard.state);
                }
                else if ((stroke.Keyboard.state & 0x01) == INTERCEPTION_KEY_UP)
                {
                    passKey = OnKeyReleased(stroke.Keyboard.code, stroke.Keyboard.state);
                }

                if (passKey)
                {
                    interception_send(keyboardContext, keyboard, ref stroke, 1);
                }
            }

            interception_destroy_context(keyboardContext);
            Debug.WriteLine("WorkerKeyboard done");
        }

        private void workerSwitchPC_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            Invoke((MethodInvoker)delegate { SwitchPCs(); });
        }
    }
}
