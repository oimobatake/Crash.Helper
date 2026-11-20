using System;
using System.Diagnostics;
using System.Linq;

namespace Crash.Helper.Memory.Fade
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
            handle = ProcessNative.OpenProcess(0x0838 | 0x0400, false, target.Id);
            if (handle == IntPtr.Zero) { process.Dispose(); ProcessNative.Check(false, "Open fade memory"); }
        }

        internal void SetEnabled(bool enabled)
        {
            if (HasExited) { applied = false; return; }
            if (enabled == applied) return;
            ProcessNative.WhilePaused(handle, process, () =>
            {
                var current = new byte[4]; UIntPtr read;
                ProcessNative.Check(ProcessNative.ReadProcessMemory(handle, address, current, (UIntPtr)4, out read) && read.ToUInt64() == 4, "Read fade instructions");
                if (!enabled && current.SequenceEqual(Original)) { applied = false; return; }
                if (!current.SequenceEqual(enabled ? Original : Disabled))
                    throw new InvalidOperationException("Fade instructions do not match. No code was overwritten.");
                long start = address.ToInt64();
                ProcessNative.RelocateThreads(process, rip =>
                {
                    if (rip <= start || rip >= start + 4) return rip;
                    if (!enabled) return start + 4;
                    throw new InvalidOperationException("A game thread is inside an unexpected fade instruction.");
                });
                uint previous;
                ProcessNative.Check(ProcessNative.VirtualProtectEx(handle, address, (UIntPtr)4, 0x40, out previous), "Make fade instructions writable");
                try
                {
                    // Retain ownership on a partial write so shutdown will attempt restoration.
                    if (enabled) applied = true;
                    UIntPtr written;
                    ProcessNative.Check(ProcessNative.WriteProcessMemory(handle, address, enabled ? Disabled : Original, (UIntPtr)4, out written) && written.ToUInt64() == 4, "Write fade instructions");
                    ProcessNative.Check(ProcessNative.FlushInstructionCache(handle, address, (UIntPtr)4), "Flush fade instructions");
                    applied = enabled;
                }
                finally
                {
                    uint ignored;
                    ProcessNative.Check(ProcessNative.VirtualProtectEx(handle, address, (UIntPtr)4, previous, out ignored), "Restore fade protection");
                }
            });
        }

        public void Dispose()
        {
            if (disposed) return;
            SetEnabled(false);
            ProcessNative.CloseHandle(handle);
            process.Dispose();
            disposed = true;
        }
    }
}
