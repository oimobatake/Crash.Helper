using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Crash.Helper.Input
{
    // Raw relative motion keeps working at screen edges and does not consume game input.
    internal sealed class MouseCameraListener : NativeWindow, IDisposable
    {
        private int processId;
        private bool registered;
        internal event Action<int, int> Moved;

        internal bool IsTargetForeground
        {
            get
            {
                uint foreground;
                GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                return registered && foreground == processId;
            }
        }

        internal void Start(int targetProcessId)
        {
            processId = targetProcessId;
            if (registered) return;
            if (Handle == IntPtr.Zero) CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
            Register(0x100, Handle); // RIDEV_INPUTSINK
            registered = true;
        }

        internal void Stop()
        {
            if (registered) Register(1, IntPtr.Zero); // RIDEV_REMOVE
            registered = false;
            processId = 0;
        }

        private static void Register(uint flags, IntPtr target)
        {
            var device = new RawInputDevice { UsagePage = 1, Usage = 2, Flags = flags, Target = target };
            if (!RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf(typeof(RawInputDevice))))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x00FF && IsTargetForeground) // WM_INPUT
            {
                try
                {
                    uint headerSize = (uint)(8 + 2 * IntPtr.Size), size = 0;
                    if (GetRawInputData(message.LParam, 0x10000003, IntPtr.Zero, ref size, headerSize) == 0 && size >= headerSize + 24)
                    {
                        var buffer = Marshal.AllocHGlobal(checked((int)size));
                        try
                        {
                            if (GetRawInputData(message.LParam, 0x10000003, buffer, ref size, headerSize) == size && Marshal.ReadInt32(buffer) == 0)
                            {
                                int offset = (int)headerSize;
                                if ((Marshal.ReadInt16(buffer, offset) & 1) == 0) // Relative mouse motion only.
                                    Moved?.Invoke(Marshal.ReadInt32(buffer, offset + 12), Marshal.ReadInt32(buffer, offset + 16));
                            }
                        }
                        finally { Marshal.FreeHGlobal(buffer); }
                    }
                }
                catch (Exception ex) { HelperLog.Error("Read mouse movement", ex); }
            }
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            try { Stop(); }
            finally { if (Handle != IntPtr.Zero) DestroyHandle(); }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice { internal ushort UsagePage, Usage; internal uint Flags; internal IntPtr Target; }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
