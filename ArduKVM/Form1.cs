using ArduKVM.Properties;
using DDCCI;
using SharpHook;
using SharpHook.Native;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ArduKVM
{
    public partial class Form1 : Form
    {

        private SerialPort serialPort = new SerialPort();

        private SimpleGlobalHook globalHook = new();
        private bool isControllingExternalPC = false;

        byte[] keyboardReport = new byte[9];
        byte[] mouseReport = new byte[5];

        short currentX, currentY = 0;
        bool stop = true;
        bool toSwitchPC = false;
        bool initialized = false;

        int portCount = 0;
        int selectedInput = 0;
        string hostInput = "";

        static DisplayService displayService = new DisplayService();
        static INodeFormatter nodeFormatter = new NodeFormatter();
        static ITokenizer tokenizer = new CapabilitiesTokenizer();
        static IParser parser = new CapabilitiesParser();

        private MonitorInfo monitor;

        uint currentInput = 0;
        uint maxInputValue = 0;

        byte wheel;

        IntPtr context;

        BlockingCollection<byte[]> queue = new BlockingCollection<byte[]>(100);

        private List<ComboBox> cbInputs = new List<ComboBox>();
        private List<ComboBox> cbPorts = new List<ComboBox>();
        private Dictionary<string, string> inputSources = new Dictionary<string, string>();
        List<PortMapping> mappings = new List<PortMapping>();

        // Interception API ±`¶q
        private const int INTERCEPTION_MOUSE = 1;
        private const int INTERCEPTION_FILTER_MOUSE_ALL = 0xFFFF;

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
        private static extern int interception_receive(IntPtr context, int device, ref MouseStroke stroke, uint nstroke);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int interception_send(IntPtr context, int device, ref MouseStroke stroke, uint nstroke);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void interception_set_filter(IntPtr context, Predicate predicate, int filter);

        private delegate int Predicate(int device);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool interception_is_mouse(int device);

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
            dxva2.GetVCPFeatureAndVCPFeatureReply(monitor.Handle, (char)0x60, IntPtr.Zero, out currentInput, out maxInputValue);

            keyboardReport[0] = 0xb3;
            mouseReport[0] = 0xb4;

            globalHook.KeyReleased += OnKeyReleased;
            globalHook.KeyPressed += OnKeyPressed;

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


            workerSerial.RunWorkerAsync();
            globalHook.RunAsync();
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

            if (!isControllingExternalPC)
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

            if (e.Data.KeyCode == KeyCode.VcInsert && keyboardReport[1] == 0x05)
            {
                toSwitchPC = true;
                return;
            }

            var key = MapKeyCode(e.Data.KeyCode);
            var modifier = MapKeyModifier(e.Data.KeyCode);

            if (modifier != 0)
            {
                if (((keyboardReport[1] & modifier) == modifier))
                {
                    keyboardReport[1] &= (byte)~modifier;
                }

                if (toSwitchPC && keyboardReport[1] == 0)
                {
                    toSwitchPC = false;
                    SwitchPCs();
                }

                SendReport(true);
                return;
            }

            toSwitchPC = false;

            for (int i = 3; i < 9; i++)
            {
                if (keyboardReport[i] == key)
                {
                    keyboardReport[i] = 0;
                }
            }

            SendReport(true);
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
            interception_destroy_context(context);
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
            timerInput.Stop();

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
            var pm = mappings[selectedInput];
            uint inputId = Convert.ToUInt32(pm.Input,16);

            if (currentInput == inputId) {
                timerInput.Start();
                return;
            }

            if (pm.Port == "This PC")
            {
                isControllingExternalPC = false;

                Array.Clear(keyboardReport, 1, keyboardReport.Length - 1);
                Array.Clear(mouseReport, 1, mouseReport.Length - 1);
                SendReport(true);
                SendReport(false);

                timerPps.Enabled = false;
                notifyIcon.BalloonTipText = "Disabled";
                notifyIcon.ShowBalloonTip(1);
                serialPort.Close();
                
            }
            else {
                Connect(pm.Port);
                isControllingExternalPC = true;
                timerPps.Enabled = true;
                notifyIcon.BalloonTipText = "Controlling PC B";
                notifyIcon.ShowBalloonTip(1);
                workerMouse.RunWorkerAsync();
            }
            displayService.SetVCPCapability(monitor, (char)0x60, (int)inputId);

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
                case KeyCode.VcPrintScreen: keycode = 0x46; break;
                case KeyCode.VcScrollLock: keycode = 0x47; break;
                case KeyCode.VcPause: keycode = 0x48; break;
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

                //NumPad
                case KeyCode.VcNumLock: keycode = 0x53; break;
                case KeyCode.VcNumPadDivide: keycode = 0x54; break;
                case KeyCode.VcNumPadMultiply: keycode = 0x55; break;
                case KeyCode.VcNumPadSubtract: keycode = 0x56; break;
                case KeyCode.VcNumPadAdd: keycode = 0x57; break;
                case KeyCode.VcNumPadEnter: keycode = 0x58; break;
                case KeyCode.VcNumPad1: keycode = 0x59; break;
                case KeyCode.VcNumPad2: keycode = 0x5A; break;
                case KeyCode.VcNumPad3: keycode = 0x5B; break;
                case KeyCode.VcNumPad4: keycode = 0x5C; break;
                case KeyCode.VcNumPad5: keycode = 0x5D; break;
                case KeyCode.VcNumPad6: keycode = 0x5E; break;
                case KeyCode.VcNumPad7: keycode = 0x5F; break;
                case KeyCode.VcNumPad8: keycode = 0x60; break;
                case KeyCode.VcNumPad9: keycode = 0x61; break;
                case KeyCode.VcNumPad0: keycode = 0x62; break;
                case KeyCode.VcNumPadDecimal: keycode = 0x63; break;

                case KeyCode.VcVolumeUp: keycode = 0x80; break;
                case KeyCode.VcVolumeDown: keycode = 0x81; break;
                case KeyCode.VcVolumeMute: keycode = 0x7F; break;
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
            for (int i = 0; i < mappings.Count; i++)
            {
                if (currentInput == Convert.ToInt32(mappings[i].Input, 16))
                {
                    selectedInput = i;
                    break;
                }
            }
        }

        private void workerMouse_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            context = interception_create_context();

            interception_set_filter(context,
                device => interception_is_mouse(device) ? 1 : 0,
                INTERCEPTION_FILTER_MOUSE_ALL);

            int device = 0;
            int mouse = 0;
            MouseStroke stroke = new MouseStroke();
            while (isControllingExternalPC)
            {
                if (mouse == 0)
                {
                    device = interception_wait_with_timeout(context, 1000);
                    if (device == 0)
                    {
                        continue;
                    }
                    if (interception_is_mouse(device))
                    {
                        mouse = device;
                    }
                }


                if (interception_wait_with_timeout(context, 1000) != mouse)
                {
                    continue;
                }

                if (interception_receive(context, mouse, ref stroke, 1) > 0)
                {
                    switch (stroke.state)
                    {
                        case INTERCEPTION_MOUSE_LEFT_BUTTON_DOWN: mouseReport[1] |= 1; SendReport(false); break;
                        case INTERCEPTION_MOUSE_LEFT_BUTTON_UP: mouseReport[1] &= unchecked((byte)~1); SendReport(false); break;
                        case INTERCEPTION_MOUSE_RIGHT_BUTTON_DOWN: mouseReport[1] |= 2; SendReport(false); break;
                        case INTERCEPTION_MOUSE_RIGHT_BUTTON_UP: mouseReport[1] &= unchecked((byte)~2); SendReport(false); break;
                        case INTERCEPTION_MOUSE_MIDDLE_BUTTON_DOWN: mouseReport[1] |= 4; SendReport(false); break;
                        case INTERCEPTION_MOUSE_MIDDLE_BUTTON_UP: mouseReport[1] &= unchecked((byte)~4); SendReport(false); break;

                        default:
                            if (stroke.rolling != 0)
                            {
                                wheel = (byte)stroke.rolling;
                            }
                            currentX += (short)stroke.x;
                            currentY += (short)stroke.y;
                            break;
                    }

                    //interception_send(context, mouse, ref stroke, 1);
                }
            }
            interception_destroy_context(context);
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
    }
}
