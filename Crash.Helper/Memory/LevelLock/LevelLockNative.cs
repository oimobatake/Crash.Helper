using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Crash.Helper.Memory.LevelLock
{
    internal static class LevelLockNative
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Region
        {
            public IntPtr BaseAddress, AllocationBase;
            public uint AllocationProtect;
            public UIntPtr RegionSize;
            public uint State, Protect, Type;
        }
        internal static void Check(bool success, string operation)
        {
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), operation);
        }
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern IntPtr OpenProcess(uint access, bool inherit, int id);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] data, UIntPtr size, out UIntPtr read);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] data, UIntPtr size, out UIntPtr written);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint type, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool VirtualProtectEx(IntPtr process, IntPtr address, UIntPtr size, uint protection, out uint previous);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern UIntPtr VirtualQueryEx(IntPtr process, IntPtr address, out Region region, UIntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern IntPtr OpenThread(uint access, bool inherit, uint id);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GetThreadContext(IntPtr thread, IntPtr context);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool SetThreadContext(IntPtr thread, IntPtr context);
        [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(IntPtr process);
        [DllImport("ntdll.dll")] private static extern int NtResumeProcess(IntPtr process);

        internal static void WhilePaused(IntPtr handle, Process process, Action action)
        {
            if (process.Id == Process.GetCurrentProcess().Id) throw new InvalidOperationException("Cannot patch the helper process itself.");
            int result = NtSuspendProcess(handle);
            if (result < 0) throw new InvalidOperationException("Could not pause game threads (0x" + result.ToString("X8") + ").");
            try { action(); }
            finally
            {
                result = NtResumeProcess(handle);
                if (result < 0 && !process.HasExited) throw new InvalidOperationException("Could not resume game threads (0x" + result.ToString("X8") + ").");
            }
        }

        internal static void RelocateThreads(Process process, long injection, long originalTest, bool installing)
        {
            RelocateThreads(process, rip =>
            {
                if (installing && rip == injection + 3) return originalTest;
                if (!installing && rip == injection + 5) return injection + 6;
                if (rip > injection && rip < injection + 6)
                    throw new InvalidOperationException("A game thread is inside an unexpected hook instruction.");
                return rip;
            });
        }

        internal static void RelocateThreads(Process process, Func<long, long> relocate)
        {
            process.Refresh();
            foreach (ProcessThread thread in process.Threads)
            {
                IntPtr handle = OpenThread(0x18, false, (uint)thread.Id);
                Check(handle != IntPtr.Zero, "Open game thread context");
                IntPtr allocation = Marshal.AllocHGlobal(1232 + 15);
                try
                {
                    IntPtr context = new IntPtr((allocation.ToInt64() + 15) & ~15L);
                    Marshal.Copy(new byte[1232], 0, context, 1232);
                    Marshal.WriteInt32(context, 48, 0x00100001); // AMD64 CONTEXT_CONTROL
                    Check(GetThreadContext(handle, context), "Read game thread context");
                    long rip = Marshal.ReadInt64(context, 248);
                    long replacement = relocate(rip);
                    if (replacement != rip)
                    {
                        Marshal.WriteInt64(context, 248, replacement);
                        Check(SetThreadContext(handle, context), "Move game thread past the patch boundary");
                    }
                }
                finally { Marshal.FreeHGlobal(allocation); CloseHandle(handle); }
            }
        }
    }
}
