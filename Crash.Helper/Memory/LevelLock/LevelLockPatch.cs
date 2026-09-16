using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Crash.Helper.Memory.LevelLock
{
    internal sealed class LevelLockPatch : IDisposable
    {
        private readonly Process process;
        private readonly IntPtr handle;
        private readonly long moduleBase, moduleEnd, mapRoot;
        private long injection, cave, control, dataPage;
        private int dataOffset, originalOffset;
        private byte[] patch;
        private bool installed;

        internal LevelLockPatch(Process process) : this(process, process.MainModule.BaseAddress.ToInt64(), process.MainModule.ModuleMemorySize,
            process.MainModule.BaseAddress.ToInt64() + 0x01A5C6D8) { }

        internal LevelLockPatch(Process process, long moduleBase, int moduleSize, long mapRoot)
        {
            if (IntPtr.Size != 8 || !process.Is64Bit()) throw new InvalidOperationException("Level lock requires a 64-bit helper and game.");
            this.process = Process.GetProcessById(process.Id);
            this.moduleBase = moduleBase;
            moduleEnd = checked(moduleBase + moduleSize);
            this.mapRoot = mapRoot;
            handle = LevelLockNative.OpenProcess(0x0838 | 0x0400, false, process.Id);
            try { LevelLockNative.Check(handle != IntPtr.Zero, "Open game memory for level lock"); }
            catch { this.process.Dispose(); throw; }
        }

        internal bool HasExited => process.HasExited;
        internal int ProcessId => process.Id;

        internal byte[] Read(long address, int size)
        {
            var bytes = new byte[size];
            UIntPtr count;
            LevelLockNative.Check(LevelLockNative.ReadProcessMemory(handle, new IntPtr(address), bytes, (UIntPtr)size, out count) && count.ToUInt64() == (ulong)size, "Read level hook memory");
            return bytes;
        }

        private void Write(long address, byte[] bytes)
        {
            UIntPtr count;
            LevelLockNative.Check(LevelLockNative.WriteProcessMemory(handle, new IntPtr(address), bytes, (UIntPtr)bytes.Length, out count) && count.ToUInt64() == (ulong)bytes.Length, "Write level hook memory");
        }

        private LevelLockNative.Region Query(long address)
        {
            LevelLockNative.Region region;
            LevelLockNative.Check(LevelLockNative.VirtualQueryEx(handle, new IntPtr(address), out region, (UIntPtr)Marshal.SizeOf(typeof(LevelLockNative.Region))) != UIntPtr.Zero, "Query level hook memory");
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
                    for (int i = 0; i <= scan.Length - LevelLockCode.Pattern.Length; i++)
                    {
                        if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                        bool equal = true;
                        for (int j = 0; j < LevelLockCode.Pattern.Length; j++)
                            if (LevelLockCode.Pattern[j].HasValue && scan[i + j] != LevelLockCode.Pattern[j].Value) { equal = false; break; }
                        if (!equal) continue;
                        long address = cursor - tail.Length + i;
                        if (match != 0) throw new InvalidOperationException("The level-lock signature is not unique. No hook was installed.");
                        match = address;
                    }
                    int overlap = Math.Min(LevelLockCode.Pattern.Length - 1, scan.Length);
                    tail = new byte[overlap];
                    Buffer.BlockCopy(scan, scan.Length - overlap, tail, 0, overlap);
                    cursor += count;
                }
            }
            if (match == 0) throw new InvalidOperationException("The level-lock signature was not found. Check the game version and disable any other level hook.");
            return match;
        }

        private long AllocateNear(long target, CancellationToken cancellation)
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
                    long last = (Math.Min(end, high) - 4096) & ~65535L;
                    if (start <= last) candidates.Add(Math.Max(start, Math.Min(last, target & ~65535L)));
                }
                cursor = end;
            }
            foreach (long address in candidates.OrderBy(value => Math.Abs(value - target)))
            {
                cancellation.ThrowIfCancellationRequested();
                IntPtr allocated = LevelLockNative.VirtualAllocEx(handle, new IntPtr(address), (UIntPtr)4096, 0x3000, 0x04);
                if (allocated != IntPtr.Zero) return allocated.ToInt64();
            }
            throw new InvalidOperationException("Could not allocate a trampoline near the level hook.");
        }

        private long AllocateData()
        {
            IntPtr allocation = LevelLockNative.VirtualAllocEx(handle, IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x04);
            LevelLockNative.Check(allocation != IntPtr.Zero, "Allocate level name storage");
            dataPage = allocation.ToInt64();
            dataOffset = 0;
            return dataPage;
        }

        private long StoreName(byte[] name)
        {
            if (dataOffset + name.Length > 4096) AllocateData();
            long address = dataPage + dataOffset;
            Write(address, name);
            dataOffset += name.Length;
            return address;
        }

        private void WriteCode(long address, byte[] bytes)
        {
            uint previous;
            LevelLockNative.Check(LevelLockNative.VirtualProtectEx(handle, new IntPtr(address), (UIntPtr)bytes.Length, 0x40, out previous), "Make level hook writable");
            try
            {
                Write(address, bytes);
                LevelLockNative.Check(LevelLockNative.FlushInstructionCache(handle, new IntPtr(address), (UIntPtr)bytes.Length), "Flush level hook instructions");
            }
            finally
            {
                uint ignored;
                LevelLockNative.Check(LevelLockNative.VirtualProtectEx(handle, new IntPtr(address), (UIntPtr)bytes.Length, previous, out ignored), "Restore level hook protection");
            }
        }

        internal void Enable(string name, CancellationToken cancellation)
        {
            var bytes = LevelLockCode.EncodeName(name);
            if (process.HasExited) throw new InvalidOperationException("The game exited before level lock was ready.");
            if (cave == 0)
            {
                injection = FindInjection(cancellation);
                long allocated = AllocateNear(injection, cancellation);
                control = AllocateData();
                dataOffset = 64;
                var code = LevelLockCode.Build(allocated, mapRoot, control, injection + 6, out originalOffset);
                Write(allocated, code);
                uint ignored;
                LevelLockNative.Check(LevelLockNative.VirtualProtectEx(handle, new IntPtr(allocated), (UIntPtr)4096, 0x20, out ignored), "Protect level trampoline");
                LevelLockNative.Check(LevelLockNative.FlushInstructionCache(handle, new IntPtr(allocated), (UIntPtr)code.Length), "Flush level trampoline");
                patch = LevelLockCode.Jump(injection, allocated, true);
                cave = allocated;
            }
            // Each name is immutable, so a thread already in the trampoline cannot see a torn update.
            long nameAddress = StoreName(bytes);
            cancellation.ThrowIfCancellationRequested();
            LevelLockNative.WhilePaused(handle, process, () =>
            {
                cancellation.ThrowIfCancellationRequested();
                var expected = installed ? patch : LevelLockCode.Original;
                if (!Read(injection, 6).SequenceEqual(expected)) throw new InvalidOperationException("The level hook was changed by another tool. No code was overwritten.");
                if (!installed) LevelLockNative.RelocateThreads(process, injection, cave + originalOffset + 3, true);
                Write(control + 8, BitConverter.GetBytes(nameAddress));
                Write(control, new byte[] { 1 });
                if (!installed)
                {
                    // Track the patch before writing so cleanup can retry after a write/protection failure.
                    installed = true;
                    try { WriteCode(injection, patch); }
                    catch
                    {
                        Write(control, new byte[] { 0 });
                        WriteCode(injection, LevelLockCode.Original);
                        installed = false;
                        throw;
                    }
                }
            });
        }

        internal void Disable()
        {
            if (cave == 0 || process.HasExited) { installed = false; return; }
            LevelLockNative.WhilePaused(handle, process, () =>
            {
                Write(control, new byte[] { 0 });
                if (!installed) return;
                byte[] current = Read(injection, 6);
                if (current.SequenceEqual(LevelLockCode.Original)) { installed = false; return; }
                if (!current.SequenceEqual(patch)) throw new InvalidOperationException("The level hook was changed by another tool. Its bytes were not overwritten.");
                LevelLockNative.RelocateThreads(process, injection, cave + originalOffset + 3, false);
                WriteCode(injection, LevelLockCode.Original);
                installed = false;
            });
        }

        public void Dispose()
        {
            Disable();
            LevelLockNative.CloseHandle(handle);
            process.Dispose();
            // Keep published code/data until the game exits: a paused thread may still be inside it.
            // Normal off/on cycles reuse this trampoline; Windows frees its pages with the game.
        }
    }
}
