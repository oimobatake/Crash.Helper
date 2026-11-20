using System;
using System.Diagnostics;
using System.Linq;

namespace Crash.Helper.Memory.Position
{
    internal sealed class LegacyPositionPatch : IDisposable
    {
        private static readonly byte[] Original = { 0x0F, 0x29, 0x06 };
        private static readonly byte[] Disabled = { 0x90, 0x90, 0x90 };
        private readonly Process process;
        private readonly IntPtr handle, address;
        private bool applied, disposed;
        internal int ProcessId => process.Id;
        internal bool HasExited => process.HasExited;

        internal LegacyPositionPatch(Process target, long address)
        {
            process = Process.GetProcessById(target.Id);
            this.address = new IntPtr(address);
            handle = ProcessNative.OpenProcess(0x0838 | 0x0400, false, target.Id);
            if (handle == IntPtr.Zero) { process.Dispose(); ProcessNative.Check(false, "Open player position memory"); }
        }

        internal void SetEnabled(bool enabled)
        {
            if (HasExited) { applied = false; return; }
            if (enabled == applied) return;
            ProcessNative.WhilePaused(handle, process, () =>
            {
                var current = new byte[3]; UIntPtr read;
                ProcessNative.Check(ProcessNative.ReadProcessMemory(handle, address, current, (UIntPtr)3, out read) && read.ToUInt64() == 3, "Read player position instructions");
                if (!enabled && current.SequenceEqual(Original)) { applied = false; return; }
                if (!current.SequenceEqual(enabled ? Original : Disabled))
                    throw new InvalidOperationException("Player position instructions do not match. No code was overwritten.");
                long start = address.ToInt64();
                ProcessNative.RelocateThreads(process, rip =>
                {
                    if (rip <= start || rip >= start + 3) return rip;
                    if (!enabled) return start + 3;
                    throw new InvalidOperationException("A game thread is inside an unexpected player position instruction.");
                });
                uint previous;
                ProcessNative.Check(ProcessNative.VirtualProtectEx(handle, address, (UIntPtr)3, 0x40, out previous), "Make player position instructions writable");
                try
                {
                    // Retain ownership on a partial write so shutdown will attempt restoration.
                    if (enabled) applied = true;
                    UIntPtr written;
                    ProcessNative.Check(ProcessNative.WriteProcessMemory(handle, address, enabled ? Disabled : Original, (UIntPtr)3, out written) && written.ToUInt64() == 3, "Write player position instructions");
                    ProcessNative.Check(ProcessNative.FlushInstructionCache(handle, address, (UIntPtr)3), "Flush player position instructions");
                    applied = enabled;
                }
                finally
                {
                    uint ignored;
                    ProcessNative.Check(ProcessNative.VirtualProtectEx(handle, address, (UIntPtr)3, previous, out ignored), "Restore player position protection");
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
