using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Crash.Helper.Input
{
    public enum GamepadButton : ushort
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
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000
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
        private readonly ushort[] prevButtons = new ushort[4];

        public event EventHandler<GamepadButtonEventArgs> ButtonPressed;
        public event EventHandler<GamepadButtonEventArgs> ButtonReleased;

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
                    if (res == 0)
                    {
                        ushort buttons = st.Gamepad.wButtons;
                        ushort prev = prevButtons[i];

                        ushort changed = (ushort)(buttons ^ prev);
                        if (changed != 0)
                        {
                            ushort pressed = (ushort)(changed & buttons);
                            ushort released = (ushort)(changed & ~buttons);

                            if (pressed != 0)
                            {
                                foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
                                {
                                    ushort mask = (ushort)b;
                                    if ((pressed & mask) != 0)
                                    {
                                        ButtonPressed?.Invoke(this, new GamepadButtonEventArgs((int)i, b));
                                    }
                                }
                            }

                            if (released != 0)
                            {
                                foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
                                {
                                    ushort mask = (ushort)b;
                                    if ((released & mask) != 0)
                                    {
                                        ButtonReleased?.Invoke(this, new GamepadButtonEventArgs((int)i, b));
                                    }
                                }
                            }

                        }

                        prevButtons[i] = buttons;
                    }
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
