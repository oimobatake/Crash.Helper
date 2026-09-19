using System;
using System.Diagnostics;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Memory
{
    internal static class FadeMemory
    {
        internal static float? Read(Process process, int[] offsets, long moduleBase = 0)
        {
            if (process == null || offsets == null || offsets.Length == 0) return null;
            try
            {
                if (process.HasExited) return null;
                long address = moduleBase == 0 ? process.MainModule.BaseAddress.ToInt64() : moduleBase;
                for (int i = 0; i < offsets.Length; i++)
                {
                    bool last = i == offsets.Length - 1;
                    int size = last ? 4 : 8;
                    var bytes = new byte[size]; UIntPtr count;
                    if (!LevelLockNative.ReadProcessMemory(process.Handle, new IntPtr(address + offsets[i]), bytes, (UIntPtr)size, out count) || count.ToUInt64() != (ulong)size) return null;
                    if (last)
                    {
                        float value = BitConverter.ToSingle(bytes, 0);
                        return float.IsNaN(value) || float.IsInfinity(value) ? (float?)null : value;
                    }
                    address = BitConverter.ToInt64(bytes, 0);
                    if (address == 0) return null;
                }
            }
            catch { return null; }
            return null;
        }

        internal static bool SuspendPositionFreeze(Process process, GameMemoryProfile profile, long moduleBase = 0)
        {
            if (profile == null) return true;
            // Read fade first: loading can become true as fade abruptly returns to zero.
            float? fade = Read(process, profile.Fade, moduleBase);
            return !fade.HasValue || fade.Value >= 1.0f || LoadingMemory.Read(process, profile.Loading, moduleBase);
        }
    }
}
