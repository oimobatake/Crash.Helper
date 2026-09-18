using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Crash.Helper.Input
{
    // Observes keyboard input without consuming it or registering exclusive hotkeys.
    internal sealed class KeyboardHotkeyListener : IDisposable
    {
        private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);
        private readonly HookCallback callback;
        private readonly HashSet<uint> pressedKeys = new HashSet<uint>();
        private IntPtr hook;
        private bool initialEnterHeld;
        public event Action<uint, KeyModifiers> KeyPressed;
        public event Action<uint> KeyReleased;

        internal bool IsHeld(uint key) => pressedKeys.Any(physical => KeyIdentity.Normalize(physical) == key) &&
            (key == (uint)Keys.LWin ? GetAsyncKeyState((int)Keys.LWin) < 0 || GetAsyncKeyState((int)Keys.RWin) < 0 : GetAsyncKeyState(KeyIdentity.VirtualKey(key)) < 0);

        public KeyboardHotkeyListener() { callback = OnKeyboardInput; }

        public void Start()
        {
            if (hook != IntPtr.Zero) return;
            pressedKeys.Clear();
            for (uint key = 1; key < 256; key++)
                if (key != (uint)Keys.ShiftKey && key != (uint)Keys.ControlKey && key != (uint)Keys.Menu && GetAsyncKeyState((int)key) < 0) pressedKeys.Add(key);
            initialEnterHeld = pressedKeys.Contains((uint)Keys.Enter);
            if (initialEnterHeld) pressedKeys.Add(KeyIdentity.NumEnter);
            hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Stop()
        {
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
            pressedKeys.Clear();
            initialEnterHeld = false;
        }

        public static KeyModifiers CurrentModifiers
        {
            get
            {
                var modifiers = KeyModifiers.None;
                if (GetAsyncKeyState((int)Keys.ControlKey) < 0) modifiers |= KeyModifiers.Control;
                if (GetAsyncKeyState((int)Keys.Menu) < 0) modifiers |= KeyModifiers.Alt;
                if (GetAsyncKeyState((int)Keys.ShiftKey) < 0) modifiers |= KeyModifiers.Shift;
                if (GetAsyncKeyState((int)Keys.LWin) < 0 || GetAsyncKeyState((int)Keys.RWin) < 0) modifiers |= KeyModifiers.Win;
                return modifiers;
            }
        }

        private IntPtr OnKeyboardInput(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                try
                {
                    uint key = unchecked((uint)Marshal.ReadInt32(data));
                    key = KeyIdentity.Encode(key, (Marshal.ReadInt32(data, 8) & 1) != 0);
                    int kind = message.ToInt32();
                    if (kind == 0x0101 || kind == 0x0105)
                    {
                        if (initialEnterHeld && KeyIdentity.VirtualKey(key) == (int)Keys.Enter)
                        {
                            pressedKeys.Remove((uint)Keys.Enter);
                            pressedKeys.Remove(KeyIdentity.NumEnter);
                            initialEnterHeld = false;
                        }
                        pressedKeys.Remove(key);
                        uint logical = KeyIdentity.Normalize(key);
                        if (!pressedKeys.Any(physical => KeyIdentity.Normalize(physical) == logical)) KeyReleased?.Invoke(logical);
                    }
                    else if (kind == 0x0100 || kind == 0x0104)
                    {
                        uint logical = KeyIdentity.Normalize(key);
                        bool wasHeld = pressedKeys.Any(physical => KeyIdentity.Normalize(physical) == logical);
                        if (pressedKeys.Add(key) && !wasHeld)
                            KeyPressed?.Invoke(logical, KeyIdentity.ModifiersForKey(logical, CurrentModifiers));
                    }
                }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
            }
            // Always forward both matching and non-matching input to other applications.
            return CallNextHookEx(hook, code, message, data);
        }

        public void Dispose() { Stop(); }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int id, HookCallback callback, IntPtr module, uint threadId);
        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }
}
