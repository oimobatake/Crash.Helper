using System;
using System.Diagnostics;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Memory
{
    internal static class LoadingMemory
    {
        // Fail closed while a level transition replaces or invalidates a pointer.
        internal static bool Read(Process process, int[] offsets, long moduleBase = 0)
        {
            if (process == null || offsets == null || offsets.Length == 0) return true;
            try
            {
                if (process.HasExited) return true;
                long address = moduleBase == 0 ? process.MainModule.BaseAddress.ToInt64() : moduleBase;
                for (int i = 0; i < offsets.Length; i++)
                {
                    int size = i == offsets.Length - 1 ? 1 : 8;
                    var bytes = new byte[size]; UIntPtr count;
                    if (!LevelLockNative.ReadProcessMemory(process.Handle, new IntPtr(address + offsets[i]), bytes, (UIntPtr)size, out count) || count.ToUInt64() != (ulong)size) return true;
                    if (size == 1) return bytes[0] != 0;
                    address = BitConverter.ToInt64(bytes, 0);
                    if (address == 0) return true;
                }
            }
            catch { return true; }
            return true;
        }
    }
}
