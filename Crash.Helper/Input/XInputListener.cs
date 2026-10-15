using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Crash.Helper.Input
{
    // Low 16 bits match XInput wButtons. Higher bits are analog inputs read as buttons
    // and PlayStation-only buttons, so every controller shares one binding mask.
    public enum GamepadButton : uint
    {
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        Guide = 0x0400,
        Touchpad = 0x0800,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
        LeftTrigger = 0x10000,
        RightTrigger = 0x20000,
        LeftStickUp = 0x40000,
        LeftStickDown = 0x80000,
        LeftStickLeft = 0x100000,
        LeftStickRight = 0x200000,
        RightStickUp = 0x400000,
        RightStickDown = 0x800000,
        RightStickLeft = 0x1000000,
        RightStickRight = 0x2000000
    }

    public static class GamepadInput
    {
        // Analog inputs count as pressed past these points so resting sticks and light trigger touches are ignored.
        public const int TriggerThreshold = 64;   // of 255
        public const double StickThreshold = 0.6; // of full tilt

        public static uint Analog(int leftTrigger, int rightTrigger, double lx, double ly, double rx, double ry)
        {
            uint mask = 0;
            if (leftTrigger > TriggerThreshold) mask |= (uint)GamepadButton.LeftTrigger;
            if (rightTrigger > TriggerThreshold) mask |= (uint)GamepadButton.RightTrigger;
            mask |= Stick(lx, ly, GamepadButton.LeftStickUp, GamepadButton.LeftStickDown, GamepadButton.LeftStickLeft, GamepadButton.LeftStickRight);
            mask |= Stick(rx, ry, GamepadButton.RightStickUp, GamepadButton.RightStickDown, GamepadButton.RightStickLeft, GamepadButton.RightStickRight);
            return mask;
        }

        // x and y are -1..1 with positive y pointing up.
        private static uint Stick(double x, double y, GamepadButton up, GamepadButton down, GamepadButton left, GamepadButton right)
        {
            uint mask = 0;
            if (y > StickThreshold) mask |= (uint)up;
            if (y < -StickThreshold) mask |= (uint)down;
            if (x < -StickThreshold) mask |= (uint)left;
            if (x > StickThreshold) mask |= (uint)right;
            return mask;
        }

        // Xbox and PlayStation names together, so a binding reads the same for either controller.
        public static string Name(GamepadButton button)
        {
            switch (button)
            {
                case GamepadButton.A: return "A/Cross";
                case GamepadButton.B: return "B/Circle";
                case GamepadButton.X: return "X/Square";
                case GamepadButton.Y: return "Y/Triangle";
                case GamepadButton.LeftShoulder: return "LB/L1";
                case GamepadButton.RightShoulder: return "RB/R1";
                case GamepadButton.LeftTrigger: return "LT/L2";
                case GamepadButton.RightTrigger: return "RT/R2";
                case GamepadButton.LeftThumb: return "LS/L3";
                case GamepadButton.RightThumb: return "RS/R3";
                case GamepadButton.Back: return "Back/Share";
                case GamepadButton.Start: return "Start/Options";
                case GamepadButton.Guide: return "PS";
                default: return button.ToString();
            }
        }
    }

    public class GamepadButtonEventArgs : EventArgs
    {
        public int UserIndex { get; }
        public GamepadButton Button { get; }
        public GamepadButtonEventArgs(int userIndex, GamepadButton button)
        {
            UserIndex = userIndex;
            Button = button;
        }
    }

    public class XInputListener : IGamepadListener
    {
        private delegate uint XInputGetStateDelegate(uint dwUserIndex, out XINPUT_STATE pState);
        private XInputGetStateDelegate XInputGetStateFunc;
        private IntPtr xinputModule = IntPtr.Zero;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        private Timer pollTimer;
        private readonly uint[] prevButtons = new uint[4];

        public event EventHandler<GamepadButtonEventArgs> ButtonPressed;
        public event EventHandler<GamepadButtonEventArgs> ButtonReleased;
        // Raised on the poll thread with the full button mask of one controller when it changes.
        public event Action<int, uint> StateChanged;

        public XInputListener(int pollIntervalMs = 20)
        {
            // Try to load a compatible xinput DLL
            string[] candidates = new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" };
            foreach (var cand in candidates)
            {
                try
                {
                    xinputModule = LoadLibrary(cand);
                    if (xinputModule != IntPtr.Zero)
                    {
                        IntPtr proc = GetProcAddress(xinputModule, "XInputGetState");
                        if (proc != IntPtr.Zero)
                        {
                            XInputGetStateFunc = (XInputGetStateDelegate)Marshal.GetDelegateForFunctionPointer(proc, typeof(XInputGetStateDelegate));
                            break;
                        }
                        else
                        {
                            FreeLibrary(xinputModule);
                            xinputModule = IntPtr.Zero;
                        }
                    }
                }
                catch { xinputModule = IntPtr.Zero; }
            }

            if (XInputGetStateFunc != null)
            {
                try
                {
                    pollTimer = new Timer(Poll, null, 0, Math.Max(8, pollIntervalMs));
                }
                catch
                {
                    pollTimer = null;
                }
            }
        }

        private void Poll(object state)
        {
            try
            {
                for (uint i = 0; i < 4; i++)
                {
                    XINPUT_STATE st = new XINPUT_STATE();
                    uint res = XInputGetStateFunc != null ? XInputGetStateFunc(i, out st) : 0xFFFFFFFF;
                    // A disconnected controller must not leave its buttons held.
                    uint buttons = res != 0 ? 0 : st.Gamepad.wButtons | GamepadInput.Analog(st.Gamepad.bLeftTrigger, st.Gamepad.bRightTrigger,
                        st.Gamepad.sThumbLX / 32767.0, st.Gamepad.sThumbLY / 32767.0, st.Gamepad.sThumbRX / 32767.0, st.Gamepad.sThumbRY / 32767.0);
                    uint prev = prevButtons[i];
                    if (buttons == prev) continue;
                    prevButtons[i] = buttons;
                    uint pressed = buttons & ~prev, released = prev & ~buttons;
                    foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
                    {
                        if ((pressed & (uint)b) != 0) ButtonPressed?.Invoke(this, new GamepadButtonEventArgs((int)i, b));
                        if ((released & (uint)b) != 0) ButtonReleased?.Invoke(this, new GamepadButtonEventArgs((int)i, b));
                    }
                    StateChanged?.Invoke((int)i, buttons);
                }
            }
            catch
            {
                // ignore polling errors
            }
        }

        public void Dispose()
        {
            pollTimer?.Dispose();
            pollTimer = null;
            if (xinputModule != IntPtr.Zero)
            {
                FreeLibrary(xinputModule);
                xinputModule = IntPtr.Zero;
            }
        }
    }
}
