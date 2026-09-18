using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Memory.Camera
{
    internal sealed class CameraSignatureNotFoundException : InvalidOperationException
    {
        internal CameraSignatureNotFoundException() : base("Camera instructions are not available yet.") { }
    }

    internal sealed class CameraProcessMemory : IDisposable
    {
        internal Process Process { get; }
        private readonly IntPtr handle;
        private readonly long moduleBase, moduleEnd;
        private readonly byte?[] pattern;

        internal CameraProcessMemory(Process process, long moduleBase, int moduleSize, byte?[] pattern)
        {
            if (IntPtr.Size != 8 || !process.Is64Bit()) throw new InvalidOperationException("Camera control requires a 64-bit helper and game.");
            Process = Process.GetProcessById(process.Id);
            this.moduleBase = moduleBase;
            moduleEnd = checked(moduleBase + moduleSize);
            this.pattern = (byte?[])pattern.Clone();
            handle = LevelLockNative.OpenProcess(0x0838 | 0x0400, false, process.Id);
            try { LevelLockNative.Check(handle != IntPtr.Zero, "Open game memory for camera control"); }
            catch { Process.Dispose(); throw; }
        }

        internal void WhilePaused(Action action) => LevelLockNative.WhilePaused(handle, Process, action);

        internal void ProtectCode(long address)
        {
            uint previous;
            LevelLockNative.Check(LevelLockNative.VirtualProtectEx(handle, new IntPtr(address), (UIntPtr)4096, 0x20, out previous), "Protect camera trampoline");
            LevelLockNative.Check(LevelLockNative.FlushInstructionCache(handle, new IntPtr(address), (UIntPtr)4096), "Flush camera trampoline");
        }

        public void Dispose()
        {
            LevelLockNative.CloseHandle(handle);
            Process.Dispose();
        }
        internal byte[] Read(long address, int size)
        {
            var bytes = new byte[size];
            UIntPtr count;
            LevelLockNative.Check(LevelLockNative.ReadProcessMemory(handle, new IntPtr(address), bytes, (UIntPtr)size, out count) && count.ToUInt64() == (ulong)size, "Read camera hook memory");
            return bytes;
        }

        internal void Write(long address, byte[] bytes)
        {
            UIntPtr count;
            LevelLockNative.Check(LevelLockNative.WriteProcessMemory(handle, new IntPtr(address), bytes, (UIntPtr)bytes.Length, out count) && count.ToUInt64() == (ulong)bytes.Length, "Write camera hook memory");
        }

        private LevelLockNative.Region Query(long address)
        {
            LevelLockNative.Region region;
            LevelLockNative.Check(LevelLockNative.VirtualQueryEx(handle, new IntPtr(address), out region, (UIntPtr)Marshal.SizeOf(typeof(LevelLockNative.Region))) != UIntPtr.Zero, "Query camera hook memory");
            if (region.RegionSize.ToUInt64() == 0) throw new InvalidOperationException("The game returned an empty memory region.");
            return region;
        }

        internal long FindInjection(CancellationToken cancellation)
        {
            long cursor = moduleBase;
            long match = 0;
            byte[] tail = new byte[0];
            while (cursor < moduleEnd)
            {
                cancellation.ThrowIfCancellationRequested();
                var region = Query(cursor);
                long end = Math.Min(moduleEnd, checked(region.BaseAddress.ToInt64() + (long)region.RegionSize.ToUInt64()));
                if (end <= cursor) throw new InvalidOperationException("The game memory region did not advance.");
                bool executable = region.State == 0x1000 && (region.Protect & 0xF0) != 0 && (region.Protect & 0x101) == 0;
                if (!executable) { tail = new byte[0]; cursor = end; continue; }
                while (cursor < end)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int count = (int)Math.Min(65536, end - cursor);
                    var block = Read(cursor, count);
                    var scan = new byte[tail.Length + block.Length];
                    Buffer.BlockCopy(tail, 0, scan, 0, tail.Length);
                    Buffer.BlockCopy(block, 0, scan, tail.Length, block.Length);
                    for (int i = 0; i <= scan.Length - pattern.Length; i++)
                    {
                        if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                        bool equal = true;
                        for (int j = 0; j < pattern.Length; j++)
                            if (pattern[j].HasValue && scan[i + j] != pattern[j].Value) { equal = false; break; }
                        long address = cursor - tail.Length + i;
                        if (!equal && scan[i] == 0xE9 && scan[i + 5] == 0x90 && scan[i + 6] == 0x90)
                        {
                            bool tailMatches = true;
                            for (int j = 7; j < pattern.Length; j++)
                                if (pattern[j].HasValue && scan[i + j] != pattern[j].Value) { tailMatches = false; break; }
                            if (tailMatches) equal = IsRecoverableCapture(address);
                        }
                        if (!equal) continue;
                        if (match != 0) throw new InvalidOperationException("The camera signature is not unique. No hook was installed.");
                        match = address;
                    }
                    int overlap = Math.Min(pattern.Length - 1, scan.Length);
                    tail = new byte[overlap];
                    Buffer.BlockCopy(scan, scan.Length - overlap, tail, 0, overlap);
                    cursor += count;
                }
            }
            if (match == 0) throw new CameraSignatureNotFoundException();
            return match;
        }

        internal bool IsRecoverableCapture(long address)
        {
            byte[] entry;
            long cave;
            byte[] owner;
            try
            {
                entry = Read(address, 7);
                if (entry[0] != 0xE9 || entry[5] != 0x90 || entry[6] != 0x90) return false;
                cave = address + 5 + BitConverter.ToInt32(entry, 1);
                // Accept only our exact trampoline, data layout, and return address.
                if (!Read(cave, 19).SequenceEqual(CameraCode.Build(cave, cave + 4096, address + 7))) return false;
                owner = Read(cave + 4096 + 16, 48);
            }
            catch (System.ComponentModel.Win32Exception) { return false; }
            catch (OverflowException) { return false; }
            if (owner.Take(CameraCode.OwnerMarker.Length).SequenceEqual(CameraCode.OwnerMarker))
            {
                int pid = BitConverter.ToInt32(owner, 24);
                long started = BitConverter.ToInt64(owner, 32);
                bool alive = false;
                try
                {
                    using (var helper = System.Diagnostics.Process.GetProcessById(pid))
                        alive = !helper.HasExited && helper.StartTime.ToUniversalTime().Ticks == started;
                }
                catch (ArgumentException) { }
                if (alive) throw new InvalidOperationException("Another running Crash Helper owns the camera hook.");
                return true;
            }
            // Compatibility with the previous unmarked Crash Helper trampoline.
            if (owner.Any(value => value != 0)) return false;
            var codeRegion = Query(cave);
            var dataRegion = Query(cave + 4096);
            if (codeRegion.BaseAddress.ToInt64() != cave || codeRegion.AllocationBase.ToInt64() != cave ||
                codeRegion.RegionSize.ToUInt64() != 4096 || codeRegion.Protect != 0x20 ||
                dataRegion.BaseAddress.ToInt64() != cave + 4096 || dataRegion.AllocationBase.ToInt64() != cave ||
                dataRegion.RegionSize.ToUInt64() != 4096 || dataRegion.Protect != 0x04) return false;
            using (var current = System.Diagnostics.Process.GetCurrentProcess())
                foreach (var helper in System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
                    using (helper)
                        if (helper.Id != current.Id && !helper.HasExited)
                            throw new InvalidOperationException("Close the other Crash Helper before recovering its camera hook.");
            return true;
        }

        internal long AllocateNear(long target, CancellationToken cancellation)
        {
            long low = Math.Max(65536, target - 0x7FFF0000L);
            long high = target + 0x7FFF0000L;
            long cursor = low;
            var candidates = new List<long>();
            while (cursor < high)
            {
                cancellation.ThrowIfCancellationRequested();
                var region = Query(cursor);
                long end = checked(region.BaseAddress.ToInt64() + (long)region.RegionSize.ToUInt64());
                if (end <= cursor) throw new InvalidOperationException("The game allocation search did not advance.");
                if (region.State == 0x10000)
                {
                    long start = (Math.Max(cursor, region.BaseAddress.ToInt64()) + 65535) & ~65535L;
                    long last = (Math.Min(end, high) - 8192) & ~65535L;
                    if (start <= last) candidates.Add(Math.Max(start, Math.Min(last, target & ~65535L)));
                }
                cursor = end;
            }
            foreach (long address in candidates.OrderBy(value => Math.Abs(value - target)))
            {
                cancellation.ThrowIfCancellationRequested();
                IntPtr allocated = LevelLockNative.VirtualAllocEx(handle, new IntPtr(address), (UIntPtr)8192, 0x3000, 0x04);
                if (allocated != IntPtr.Zero) return allocated.ToInt64();
            }
            throw new InvalidOperationException("Could not allocate a trampoline near the camera hook.");
        }

        internal void WriteCode(long address, byte[] bytes)
        {
            uint previous;
            LevelLockNative.Check(LevelLockNative.VirtualProtectEx(handle, new IntPtr(address), (UIntPtr)bytes.Length, 0x40, out previous), "Make camera hook writable");
            try
            {
                Write(address, bytes);
                LevelLockNative.Check(LevelLockNative.FlushInstructionCache(handle, new IntPtr(address), (UIntPtr)bytes.Length), "Flush camera hook instructions");
            }
            finally
            {
                uint ignored;
                LevelLockNative.Check(LevelLockNative.VirtualProtectEx(handle, new IntPtr(address), (UIntPtr)bytes.Length, previous, out ignored), "Restore camera hook protection");
            }
        }

    }
}
