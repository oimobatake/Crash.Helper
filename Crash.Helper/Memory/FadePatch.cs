using System;
using System.Diagnostics;
using System.Linq;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Memory
{
    internal sealed class FadePatch : IDisposable
    {
        private readonly byte[] Original;
        private static readonly byte[] Disabled = { 0x90, 0x90, 0x90, 0x90 };
        private readonly Process process;
        private readonly IntPtr handle, address;
        private bool applied, disposed;
        internal int ProcessId => process.Id;
        internal bool HasExited => process.HasExited;

        internal FadePatch(Process target, long address, byte[] original)
        {
            Original = (byte[])original.Clone();
            process = Process.GetProcessById(target.Id);
            this.address = new IntPtr(address);
            handle = LevelLockNative.OpenProcess(0x0838 | 0x0400, false, target.Id);
            if (handle == IntPtr.Zero) { process.Dispose(); LevelLockNative.Check(false, "Open fade memory"); }
        }

        internal void SetEnabled(bool enabled)
        {
            if (HasExited) { applied = false; return; }
            if (enabled == applied) return;
            LevelLockNative.WhilePaused(handle, process, () =>
            {
                var current = new byte[4]; UIntPtr read;
                LevelLockNative.Check(LevelLockNative.ReadProcessMemory(handle, address, current, (UIntPtr)4, out read) && read.ToUInt64() == 4, "Read fade instructions");
                if (!enabled && current.SequenceEqual(Original)) { applied = false; return; }
                if (!current.SequenceEqual(enabled ? Original : Disabled))
                    throw new InvalidOperationException("Fade instructions do not match. No code was overwritten.");
                long start = address.ToInt64();
                LevelLockNative.RelocateThreads(process, rip =>
                {
                    if (rip <= start || rip >= start + 4) return rip;
                    if (!enabled) return start + 4;
                    throw new InvalidOperationException("A game thread is inside an unexpected fade instruction.");
                });
                uint previous;
                LevelLockNative.Check(LevelLockNative.VirtualProtectEx(handle, address, (UIntPtr)4, 0x40, out previous), "Make fade instructions writable");
                try
                {
                    // Retain ownership on a partial write so shutdown will attempt restoration.
                    if (enabled) applied = true;
                    UIntPtr written;
                    LevelLockNative.Check(LevelLockNative.WriteProcessMemory(handle, address, enabled ? Disabled : Original, (UIntPtr)4, out written) && written.ToUInt64() == 4, "Write fade instructions");
                    LevelLockNative.Check(LevelLockNative.FlushInstructionCache(handle, address, (UIntPtr)4), "Flush fade instructions");
                    applied = enabled;
                }
                finally
                {
                    uint ignored;
                    LevelLockNative.Check(LevelLockNative.VirtualProtectEx(handle, address, (UIntPtr)4, previous, out ignored), "Restore fade protection");
                }
            });
        }

        public void Dispose()
        {
            if (disposed) return;
            SetEnabled(false);
            LevelLockNative.CloseHandle(handle);
            process.Dispose();
            disposed = true;
        }
    }
}
