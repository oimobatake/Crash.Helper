using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Crash.Helper.Memory.Runtime
{
    internal sealed class RuntimeGuard
    {
        internal long Address, Value;
        internal int Size;
        internal uint Mask;
        internal RuntimeGuard(long address, long value, int size = 8, uint mask = uint.MaxValue)
        { Address = address; Value = value; Size = size; Mask = mask; }
    }

    internal sealed partial class RuntimeMemory : IDisposable
    {
        internal readonly Process Process;
        internal readonly IntPtr Handle;
        internal readonly long Base;
        internal RuntimeMemory(Process target)
        {
            Process = System.Diagnostics.Process.GetProcessById(target.Id);
            if (Process.MainModule.ModuleMemorySize != GameMemoryProfile.Steam.ModuleSize)
            { Process.Dispose(); throw new NotSupportedException("Engine transforms require the inspected Steam build."); }
            Base = Process.MainModule.BaseAddress.ToInt64();
            Handle = ProcessNative.OpenProcess(0xC38, false, Process.Id);
            if (Handle == IntPtr.Zero) { Process.Dispose(); ProcessNative.Check(false, "Open engine transform memory"); }
        }
        internal byte[] Read(long address, int size)
        {
            var bytes = new byte[size]; UIntPtr count;
            ProcessNative.Check(ProcessNative.ReadProcessMemory(Handle, new IntPtr(address), bytes, (UIntPtr)size, out count) && count.ToUInt64() == (ulong)size, "Read engine transform");
            return bytes;
        }
        internal long Number(long address, int size = 8)
        {
            var bytes = Read(address, size);
            return size == 8 ? BitConverter.ToInt64(bytes, 0) : size == 4 ? BitConverter.ToUInt32(bytes, 0) : bytes[0];
        }
        internal void Write(long address, byte[] bytes)
        {
            UIntPtr count;
            ProcessNative.Check(ProcessNative.WriteProcessMemory(Handle, new IntPtr(address), bytes, (UIntPtr)bytes.Length, out count) && count.ToUInt64() == (ulong)bytes.Length, "Write engine transform command");
        }
        internal void Require(long address, byte[] bytes)
        { if (!Read(address, bytes.Length).SequenceEqual(bytes)) throw new InvalidOperationException("Engine transform instructions do not match this game build."); }
        internal void WhilePaused(Action action) => ProcessNative.WhilePaused(Handle, Process, action);
        internal void WriteCode(long address, byte[] bytes)
            => ProcessNative.WriteCode(Handle, address, bytes, "transform");
        internal long Allocate(long anchor, int size = 8192)
        {
            anchor &= ~65535L;
            for (long distance = 65536; distance < 0x10000000; distance += 65536)
                foreach (long hint in new[] { anchor + distance, anchor - distance })
                {
                    long result = ProcessNative.VirtualAllocEx(Handle, new IntPtr(hint), (UIntPtr)size, 0x3000, 4).ToInt64();
                    if (result != 0) return result;
                }
            throw new InvalidOperationException("Could not allocate the engine transform dispatcher.");
        }
        internal void Protect(long address)
        {
            uint previous;
            ProcessNative.Check(ProcessNative.VirtualProtectEx(Handle, new IntPtr(address), (UIntPtr)4096, 0x20, out previous), "Protect transform dispatcher");
            ProcessNative.Check(ProcessNative.FlushInstructionCache(Handle, new IntPtr(address), (UIntPtr)4096), "Flush transform dispatcher");
        }
        internal bool Matches(IEnumerable<RuntimeGuard> guards)
        {
            try
            {
                var entries = guards as IList<RuntimeGuard> ?? guards.ToArray();
                for (int i = 0; i < entries.Count; i++)
                {
                    var guard = entries[i]; int count = 1;
                    if (guard.Size == 8)
                    {
                        while (i + count < entries.Count && entries[i + count].Size == 8 && entries[i + count].Address == guard.Address + count * 8L) count++;
                        var bytes = Read(guard.Address, count * 8);
                        for (int j = 0; j < count; j++) if (BitConverter.ToInt64(bytes, j * 8) != entries[i + j].Value) return false;
                        i += count - 1;
                    }
                    else if ((Number(guard.Address, guard.Size) & guard.Mask) != guard.Value) return false;
                }
                return true;
            }
            catch (Win32Exception) { return false; }
        }
        internal long Capture(List<RuntimeGuard> guards, long address, int size = 8, uint mask = uint.MaxValue)
        {
            long value = Number(address, size);
            if (size != 8) value &= mask;
            guards.Add(new RuntimeGuard(address, value, size, mask)); return value;
        }
        internal long Pointer(List<RuntimeGuard> guards, long address)
        { long value = Capture(guards, address); if (value < 0x10000) throw new InvalidOperationException("The engine object is unavailable."); return value; }
        private string Text(long address) => Encoding.UTF8.GetString(Read(address, 180)).Split('\0')[0];
        private long Meta(long address)
        {
            long getter = Number(Number(address)); var code = Read(getter, 16);
            if (code[0] == 0x48 && code[1] == 0x8B && code[2] == 0x05) return Number(getter + 7 + BitConverter.ToInt32(code, 3));
            if (code[0] == 0x48 && code[1] == 0x8B && code[2] == 0x41 && code[4] == 0xC3) return Number(address + code[3]);
            throw new InvalidOperationException("The engine object type is unavailable.");
        }
        internal string TypeName(long address) => Text(Number(Meta(address) + 0x10));
        private void Type(long address, string expected)
        { if (TypeName(address) != expected) throw new InvalidOperationException("The engine object has been replaced."); }
        internal long[] List(long list, List<RuntimeGuard> guards, int limit, bool captureEntries = true)
        {
            int count = checked((int)Capture(guards, list + 0x10, 4));
            long capacity = Number(list + 0x14, 4), bytes = Number(list + 0x18, 4);
            if (count < 0 || count > limit || capacity < count || bytes < count * 8L) throw new InvalidOperationException("The engine list is unavailable.");
            if (count == 0) return new long[0];
            long array = Pointer(guards, list + 0x20);
            if (!captureEntries)
            {
                var entries = Read(array, count * 8);
                return Enumerable.Range(0, count).Select(i => BitConverter.ToInt64(entries, i * 8)).ToArray();
            }
            return Enumerable.Range(0, count).Select(i => Capture(guards, array + i * 8)).ToArray();
        }
        internal float[] Vector(long address)
        {
            var bytes = Read(address, 12);
            return Enumerable.Range(0, 3).Select(i => BitConverter.ToSingle(bytes, i * 4)).ToArray();
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void Dispose() { ProcessNative.CloseHandle(Handle); Process.Dispose(); }
    }
}
