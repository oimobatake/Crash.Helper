using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Crash.Helper.Input
{
    // Reads DualShock 4 and DualSense controllers directly through Raw Input, because they are not
    // XInput devices unless a tool such as Steam Input or DS4Windows emulates one for this process.
    internal sealed class PlayStationListener : NativeWindow, IDisposable
    {
        private const int SonyVendor = 0x054C;
        private static readonly HashSet<int> DualShock4 = new HashSet<int> { 0x05C4, 0x09CC, 0x0BA0 };
        private static readonly HashSet<int> DualSense = new HashSet<int> { 0x0CE6, 0x0DF2 };

        // 0 = not a supported controller, 4 = DualShock 4, 5 = DualSense.
        private readonly Dictionary<IntPtr, int> models = new Dictionary<IntPtr, int>();
        private readonly Dictionary<IntPtr, uint> states = new Dictionary<IntPtr, uint>();

        // Raised on the UI thread with the full button mask of one controller when it changes.
        internal event Action<IntPtr, uint> StateChanged;

        internal PlayStationListener()
        {
            CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
            // Gamepads, delivered while the helper is in the background, with arrival and removal notices.
            var device = new RawInputDevice { UsagePage = 1, Usage = 5, Flags = 0x100 | 0x2000, Target = Handle };
            if (!RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf(typeof(RawInputDevice))))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        protected override void WndProc(ref Message message)
        {
            try
            {
                if (message.Msg == 0x00FF) Read(message.LParam); // WM_INPUT
                else if (message.Msg == 0x00FE && message.WParam.ToInt64() == 2) Publish(message.LParam, 0); // WM_INPUT_DEVICE_CHANGE removal
            }
            catch (Exception ex) { HelperLog.Error("Read PlayStation controller", ex); }
            base.WndProc(ref message);
        }

        private void Read(IntPtr input)
        {
            uint headerSize = (uint)(8 + 2 * IntPtr.Size), size = 0;
            if (GetRawInputData(input, 0x10000003, IntPtr.Zero, ref size, headerSize) != 0 || size < headerSize + 8) return;
            var buffer = new byte[size];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                if (GetRawInputData(input, 0x10000003, handle.AddrOfPinnedObject(), ref size, headerSize) != size) return;
            }
            finally { handle.Free(); }
            if (BitConverter.ToInt32(buffer, 0) != 2) return; // RIM_TYPEHID
            var device = IntPtr.Size == 8 ? new IntPtr(BitConverter.ToInt64(buffer, 8)) : new IntPtr(BitConverter.ToInt32(buffer, 8));
            int model = Model(device);
            if (model == 0) return;
            int offset = (int)headerSize;
            int reportSize = BitConverter.ToInt32(buffer, offset), count = BitConverter.ToInt32(buffer, offset + 4);
            if (reportSize <= 0 || count <= 0 || offset + 8 + reportSize * count > buffer.Length) return;
            // Several reports can arrive together; the last one is the current state.
            int start = offset + 8 + reportSize * (count - 1);
            uint? mask = Decode(model, buffer, start, reportSize);
            if (mask.HasValue) Publish(device, mask.Value);
        }

        private void Publish(IntPtr device, uint mask)
        {
            uint previous;
            if (states.TryGetValue(device, out previous) && previous == mask) return;
            if (mask == 0 && !states.ContainsKey(device)) return;
            states[device] = mask;
            StateChanged?.Invoke(device, mask);
        }

        // Offsets are from the report ID byte, which Raw Input includes.
        internal static uint? Decode(int model, byte[] data, int start, int length)
        {
            int id = data[start];
            int sticks, buttons, triggers;
            if (model == 5 && id == 0x01 && length >= 64) { sticks = 1; triggers = 5; buttons = 8; }      // DualSense USB
            else if (model == 5 && id == 0x31) { sticks = 2; triggers = 6; buttons = 9; }                // DualSense Bluetooth
            else if (model == 4 && id == 0x11) { sticks = 3; buttons = 7; triggers = 10; }               // DualShock 4 Bluetooth
            else if (id == 0x01) { sticks = 1; buttons = 5; triggers = 8; }                               // DualShock 4 USB, short Bluetooth reports
            else return null;
            if (length <= Math.Max(buttons + 2, triggers + 1)) return null;
            byte b0 = data[start + buttons], b1 = data[start + buttons + 1], b2 = data[start + buttons + 2];
            uint mask = 0;
            switch (b0 & 0x0F) // D-pad hat: 0 = up, clockwise, 8 = released.
            {
                case 0: mask |= (uint)GamepadButton.DPadUp; break;
                case 1: mask |= (uint)(GamepadButton.DPadUp | GamepadButton.DPadRight); break;
                case 2: mask |= (uint)GamepadButton.DPadRight; break;
                case 3: mask |= (uint)(GamepadButton.DPadDown | GamepadButton.DPadRight); break;
                case 4: mask |= (uint)GamepadButton.DPadDown; break;
                case 5: mask |= (uint)(GamepadButton.DPadDown | GamepadButton.DPadLeft); break;
                case 6: mask |= (uint)GamepadButton.DPadLeft; break;
                case 7: mask |= (uint)(GamepadButton.DPadUp | GamepadButton.DPadLeft); break;
            }
            if ((b0 & 0x10) != 0) mask |= (uint)GamepadButton.X;  // Square
            if ((b0 & 0x20) != 0) mask |= (uint)GamepadButton.A;  // Cross
            if ((b0 & 0x40) != 0) mask |= (uint)GamepadButton.B;  // Circle
            if ((b0 & 0x80) != 0) mask |= (uint)GamepadButton.Y;  // Triangle
            if ((b1 & 0x01) != 0) mask |= (uint)GamepadButton.LeftShoulder;
            if ((b1 & 0x02) != 0) mask |= (uint)GamepadButton.RightShoulder;
            if ((b1 & 0x10) != 0) mask |= (uint)GamepadButton.Back;   // Share / Create
            if ((b1 & 0x20) != 0) mask |= (uint)GamepadButton.Start;  // Options
            if ((b1 & 0x40) != 0) mask |= (uint)GamepadButton.LeftThumb;
            if ((b1 & 0x80) != 0) mask |= (uint)GamepadButton.RightThumb;
            if ((b2 & 0x01) != 0) mask |= (uint)GamepadButton.Guide;
            if ((b2 & 0x02) != 0) mask |= (uint)GamepadButton.Touchpad;
            // Sticks are 0-255 with 128 centered and Y growing downward.
            Func<int, double> axis = i => (data[start + sticks + i] - 128) / 127.0;
            mask |= GamepadInput.Analog(data[start + triggers], data[start + triggers + 1], axis(0), -axis(1), axis(2), -axis(3));
            return mask;
        }

        private int Model(IntPtr device)
        {
            int model;
            if (models.TryGetValue(device, out model)) return model;
            model = 0;
            var info = new byte[32];
            BitConverter.GetBytes(info.Length).CopyTo(info, 0);
            uint size = (uint)info.Length;
            if (GetRawInputDeviceInfo(device, 0x2000000B, info, ref size) > 0 && BitConverter.ToInt32(info, 4) == 2) // RIDI_DEVICEINFO, RIM_TYPEHID
            {
                int vendor = BitConverter.ToInt32(info, 8), product = BitConverter.ToInt32(info, 12);
                if (vendor == SonyVendor) model = DualShock4.Contains(product) ? 4 : DualSense.Contains(product) ? 5 : 0;
            }
            models[device] = model;
            return model;
        }

        public void Dispose()
        {
            try
            {
                var device = new RawInputDevice { UsagePage = 1, Usage = 5, Flags = 1, Target = IntPtr.Zero }; // RIDEV_REMOVE
                RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf(typeof(RawInputDevice)));
            }
            finally { if (Handle != IntPtr.Zero) DestroyHandle(); }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice { internal ushort UsagePage, Usage; internal uint Flags; internal IntPtr Target; }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, byte[] data, ref uint size);
    }
}
