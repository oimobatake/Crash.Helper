using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Crash.Helper.Input
{
    internal static class ForegroundApplication
    {
        internal static bool IsGameOrHelper(Process game)
        {
            uint foreground;
            GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
            if (foreground == GetCurrentProcessId()) return true;
            try { return game != null && !game.HasExited && foreground == game.Id; }
            catch (InvalidOperationException) { return false; }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
    }
}
