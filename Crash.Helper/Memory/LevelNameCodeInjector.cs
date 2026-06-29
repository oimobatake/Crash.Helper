using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Crash.Helper.Memory
{
    internal sealed class LevelNameCodeInjector
    {
        private const string Signature = "4188004885D274??4983C8FF";
        private const string LoadMapPrefix = "loadmap ";
        private const int HookLength = 6;
        private const int LevelNameBufferSize = 64;
        private const int HitFlagSize = 4;
        private const int HitCountSize = 4;
        private const int CapturedSourceBufferSize = 64;
        private const int NewMemSize = 0x1000;
        private const long MaxRel32Distance = int.MaxValue;
        private const int ModuleLevelNamePointerOffset = 0x01A5C6D8;

        private readonly string moduleName;
        private Process process;

        private IntPtr injectAddress = IntPtr.Zero;
        private IntPtr returnAddress = IntPtr.Zero;
        private IntPtr trampolineAddress = IntPtr.Zero;
        private IntPtr levelNameAddress = IntPtr.Zero;
        private IntPtr hitFlagAddress = IntPtr.Zero;
        private IntPtr hitCountAddress = IntPtr.Zero;
        private IntPtr capturedSourceAddress = IntPtr.Zero;
        private byte[] originalBytes;
        private bool awaitingOneShotHit;
        private string pendingLevelName;
        private string pendingCommand;

        public LevelNameCodeInjector(string moduleName)
        {
            this.moduleName = moduleName;
        }

        public bool IsEnabled => injectAddress != IntPtr.Zero;
        public bool IsPendingOneShot => awaitingOneShotHit;
        public string PendingLevelName => pendingLevelName;

        public void Bind(Process process)
        {
            this.process = process;
        }

        public bool Enable(string levelName, out string error)
        {
            error = null;

            if (process == null || process.HasExited)
            {
                error = "Process is not available.";
                return false;
            }

            if (!IsEnabled)
            {
                if (!InstallHook(out error))
                {
                    Disable(out _);
                    return false;
                }
            }

            string command = BuildLoadMapCommand(levelName);
            if (!WriteLevelName(command, out error))
            {
                return false;
            }

            if (!WriteHitFlag(0, out error))
            {
                return false;
            }

            if (!WriteHitCount(0, out error))
            {
                return false;
            }

            if (!WriteCapturedSource(string.Empty, out error))
            {
                return false;
            }

            pendingLevelName = levelName;
            pendingCommand = command;
            awaitingOneShotHit = true;
            Trace.WriteLine($"[LevelNameCodeInjector] Reliable write armed. map='{levelName}' command='{command}'");

            return true;
        }

        public bool TryConsumeOneShotHit(out string levelName, out int hitCount, out string capturedSource, out string error)
        {
            levelName = null;
            hitCount = 0;
            capturedSource = null;
            error = null;

            if (!awaitingOneShotHit)
            {
                return false;
            }

            if (!ReadHitFlag(out var hitFlag, out error))
            {
                return false;
            }

            if (hitFlag == 0)
            {
                return false;
            }

            if (!ReadHitCount(out hitCount, out error))
            {
                return false;
            }

            if (!ReadCapturedSource(out capturedSource, out error))
            {
                return false;
            }

            levelName = pendingLevelName;
            awaitingOneShotHit = false;
            Trace.WriteLine($"[LevelNameCodeInjector] One-shot hit detected. map='{levelName}', command='{pendingCommand}', hitCount={hitCount}, source='{capturedSource}'");

            if (!WriteHitFlag(0, out error))
            {
                return false;
            }

            return true;
        }

        public bool TryConsumeOneShotHit(out string levelName, out string error)
        {
            return TryConsumeOneShotHit(out levelName, out _, out _, out error);
        }

        public bool Disable(out string error)
        {
            error = null;
            bool ok = true;

            if (process != null && !process.HasExited && injectAddress != IntPtr.Zero && originalBytes != null)
            {
                if (!WriteBytes(injectAddress, originalBytes, out var writeError))
                {
                    ok = false;
                    error = writeError;
                }
            }

            if (process != null && !process.HasExited && trampolineAddress != IntPtr.Zero)
            {
                if (!NativeMethods.VirtualFreeEx(process.Handle, trampolineAddress, IntPtr.Zero, NativeMethods.MEM_RELEASE))
                {
                    ok = false;
                    if (error == null) error = "Failed to free trampoline memory.";
                }
            }

            if (process != null && !process.HasExited && levelNameAddress != IntPtr.Zero)
            {
                if (!NativeMethods.VirtualFreeEx(process.Handle, levelNameAddress, IntPtr.Zero, NativeMethods.MEM_RELEASE))
                {
                    ok = false;
                    if (error == null) error = "Failed to free level name memory.";
                }
            }

            if (process != null && !process.HasExited && hitFlagAddress != IntPtr.Zero)
            {
                if (!NativeMethods.VirtualFreeEx(process.Handle, hitFlagAddress, IntPtr.Zero, NativeMethods.MEM_RELEASE))
                {
                    ok = false;
                    if (error == null) error = "Failed to free hit flag memory.";
                }
            }

            if (process != null && !process.HasExited && hitCountAddress != IntPtr.Zero)
            {
                if (!NativeMethods.VirtualFreeEx(process.Handle, hitCountAddress, IntPtr.Zero, NativeMethods.MEM_RELEASE))
                {
                    ok = false;
                    if (error == null) error = "Failed to free hit count memory.";
                }
            }

            if (process != null && !process.HasExited && capturedSourceAddress != IntPtr.Zero)
            {
                if (!NativeMethods.VirtualFreeEx(process.Handle, capturedSourceAddress, IntPtr.Zero, NativeMethods.MEM_RELEASE))
                {
                    ok = false;
                    if (error == null) error = "Failed to free captured source memory.";
                }
            }

            injectAddress = IntPtr.Zero;
            returnAddress = IntPtr.Zero;
            trampolineAddress = IntPtr.Zero;
            levelNameAddress = IntPtr.Zero;
            hitFlagAddress = IntPtr.Zero;
            hitCountAddress = IntPtr.Zero;
            capturedSourceAddress = IntPtr.Zero;
            originalBytes = null;
            awaitingOneShotHit = false;
            pendingLevelName = null;
            pendingCommand = null;

            return ok;
        }

        public void Unbind()
        {
            try { Disable(out _); } catch { }
            process = null;
        }

        private bool InstallHook(out string error)
        {
            error = null;

            if (!TryFindUniqueInjectionAddress(out var foundInjectAddress, out var moduleBase, out error))
            {
                return false;
            }

            if (!ReadBytes(foundInjectAddress, HookLength, out originalBytes, out error))
            {
                return false;
            }

            levelNameAddress = NativeMethods.VirtualAllocEx(
                process.Handle,
                IntPtr.Zero,
                new IntPtr(LevelNameBufferSize),
                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                NativeMethods.PAGE_READWRITE);

            if (levelNameAddress == IntPtr.Zero)
            {
                error = "Failed to allocate LevelName buffer.";
                return false;
            }

            hitFlagAddress = NativeMethods.VirtualAllocEx(
                process.Handle,
                IntPtr.Zero,
                new IntPtr(HitFlagSize),
                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                NativeMethods.PAGE_READWRITE);

            if (hitFlagAddress == IntPtr.Zero)
            {
                error = "Failed to allocate hit flag buffer.";
                return false;
            }

            hitCountAddress = NativeMethods.VirtualAllocEx(
                process.Handle,
                IntPtr.Zero,
                new IntPtr(HitCountSize),
                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                NativeMethods.PAGE_READWRITE);

            if (hitCountAddress == IntPtr.Zero)
            {
                error = "Failed to allocate hit count buffer.";
                return false;
            }

            capturedSourceAddress = NativeMethods.VirtualAllocEx(
                process.Handle,
                IntPtr.Zero,
                new IntPtr(CapturedSourceBufferSize),
                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                NativeMethods.PAGE_READWRITE);

            if (capturedSourceAddress == IntPtr.Zero)
            {
                error = "Failed to allocate captured source buffer.";
                return false;
            }

            trampolineAddress = AllocateNear(foundInjectAddress, NewMemSize);
            if (trampolineAddress == IntPtr.Zero)
            {
                error = "Failed to allocate trampoline memory within rel32 range.";
                return false;
            }

            returnAddress = foundInjectAddress + HookLength;

            if (!BuildTrampoline(moduleBase, out var trampoline, out error))
            {
                return false;
            }

            if (!WriteBytes(trampolineAddress, trampoline, out error))
            {
                return false;
            }

            if (!TryGetRel32(foundInjectAddress, trampolineAddress, 5, out var relToTrampoline))
            {
                error = "Trampoline is out of rel32 range.";
                return false;
            }

            var hookPatch = new byte[HookLength];
            hookPatch[0] = 0xE9;
            Array.Copy(BitConverter.GetBytes(relToTrampoline), 0, hookPatch, 1, 4);
            hookPatch[5] = 0x90;

            if (!WriteBytes(foundInjectAddress, hookPatch, out error))
            {
                return false;
            }

            injectAddress = foundInjectAddress;
            return true;
        }

        private bool BuildTrampoline(IntPtr moduleBase, out byte[] bytes, out string error)
        {
            error = null;
            var code = new List<byte>(384);

            long mapPointerAddress = moduleBase.ToInt64() + ModuleLevelNamePointerOffset;

            code.Add(0x50); // push rax
            AppendMovRaxFromAbsolute(code, (ulong)mapPointerAddress);
            code.AddRange(new byte[] { 0x48, 0x8D, 0x40, 0x18 }); // lea rax,[rax+18]
            code.AddRange(new byte[] { 0x49, 0x3B, 0xC0 }); // cmp r8,rax

            code.AddRange(new byte[] { 0x0F, 0x85 }); // jne original
            int jneRelIndex = code.Count;
            code.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

            code.AddRange(new byte[] { 0x48, 0x85, 0xD2 }); // test rdx,rdx
            code.AddRange(new byte[] { 0x0F, 0x84 }); // je original
            int jeNullRdxRelIndex = code.Count;
            code.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

            AppendMovRaxImmediate(code, LoadMapPrefixQword);
            code.AddRange(new byte[] { 0x48, 0x39, 0x02 }); // cmp [rdx],rax
            code.AddRange(new byte[] { 0x0F, 0x85 }); // jne original
            int jnePrefixMismatchRelIndex = code.Count;
            code.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

            AppendMovRaxImmediate(code, (ulong)hitFlagAddress.ToInt64());
            code.AddRange(new byte[] { 0x83, 0x38, 0x00 }); // cmp dword ptr [rax],0
            code.AddRange(new byte[] { 0x0F, 0x85 }); // jne original
            int jneAlreadyHitRelIndex = code.Count;
            code.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

            AppendMovRaxImmediate(code, (ulong)hitCountAddress.ToInt64());
            code.AddRange(new byte[] { 0xFF, 0x00 }); // inc dword ptr [rax]

            int[] sourceOffsets = { 0x00, 0x08, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38 };
            foreach (var offset in sourceOffsets)
            {
                if (offset == 0)
                {
                    code.AddRange(new byte[] { 0x48, 0x8B, 0x02 }); // mov rax,[rdx]
                }
                else
                {
                    code.AddRange(new byte[] { 0x48, 0x8B, 0x42, (byte)offset }); // mov rax,[rdx+xx]
                }

                AppendMovAbsoluteFromRax(code, (ulong)(capturedSourceAddress.ToInt64() + offset));
            }

            int[] offsets = { 0x00, 0x08, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38 };
            foreach (var offset in offsets)
            {
                AppendMovRaxFromAbsolute(code, (ulong)(levelNameAddress.ToInt64() + offset));
                if (offset == 0)
                {
                    code.AddRange(new byte[] { 0x48, 0x89, 0x02 }); // mov [rdx],rax
                }
                else
                {
                    code.AddRange(new byte[] { 0x48, 0x89, 0x42, (byte)offset }); // mov [rdx+xx],rax
                }
            }

            AppendMovRaxImmediate(code, (ulong)hitFlagAddress.ToInt64());
            code.AddRange(new byte[] { 0xC7, 0x00, 0x01, 0x00, 0x00, 0x00 }); // mov dword ptr [rax],1

            int originalLabelOffset = code.Count;
            code.Add(0x58); // pop rax
            code.AddRange(new byte[] { 0x41, 0x88, 0x00 }); // mov [r8],al
            code.AddRange(new byte[] { 0x48, 0x85, 0xD2 }); // test rdx,rdx
            code.Add(0xE9); // jmp return
            int jmpReturnRelIndex = code.Count;
            code.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

            if (!TryGetRel32(
                trampolineAddress + (jneRelIndex - 2),
                trampolineAddress + originalLabelOffset,
                6,
                out var jneRel))
            {
                error = "Failed to resolve JNE rel32 in trampoline.";
                bytes = null;
                return false;
            }

            if (!TryGetRel32(
                trampolineAddress + (jeNullRdxRelIndex - 2),
                trampolineAddress + originalLabelOffset,
                6,
                out var jeNullRdxRel))
            {
                error = "Failed to resolve JE rel32 in trampoline.";
                bytes = null;
                return false;
            }

            if (!TryGetRel32(
                trampolineAddress + (jnePrefixMismatchRelIndex - 2),
                trampolineAddress + originalLabelOffset,
                6,
                out var jnePrefixMismatchRel))
            {
                error = "Failed to resolve prefix-mismatch JNE rel32 in trampoline.";
                bytes = null;
                return false;
            }

            if (!TryGetRel32(
                trampolineAddress + (jneAlreadyHitRelIndex - 2),
                trampolineAddress + originalLabelOffset,
                6,
                out var jneAlreadyHitRel))
            {
                error = "Failed to resolve already-hit JNE rel32 in trampoline.";
                bytes = null;
                return false;
            }

            if (!TryGetRel32(
                trampolineAddress + (jmpReturnRelIndex - 1),
                returnAddress,
                5,
                out var jmpReturnRel))
            {
                error = "Failed to resolve JMP return rel32 in trampoline.";
                bytes = null;
                return false;
            }

            WriteInt32(code, jneRelIndex, jneRel);
            WriteInt32(code, jeNullRdxRelIndex, jeNullRdxRel);
            WriteInt32(code, jnePrefixMismatchRelIndex, jnePrefixMismatchRel);
            WriteInt32(code, jneAlreadyHitRelIndex, jneAlreadyHitRel);
            WriteInt32(code, jmpReturnRelIndex, jmpReturnRel);

            bytes = code.ToArray();
            return true;
        }

        private bool WriteLevelName(string levelName, out string error)
        {
            error = null;
            if (levelNameAddress == IntPtr.Zero)
            {
                error = "LevelName buffer is not initialized.";
                return false;
            }

            if (levelName == null)
            {
                levelName = string.Empty;
            }

            var bytes = new byte[LevelNameBufferSize];
            var encoded = Encoding.UTF8.GetBytes(levelName);
            if (encoded.Length >= LevelNameBufferSize)
            {
                error = $"Injected command is too long for LevelName buffer. length={encoded.Length}, max={LevelNameBufferSize - 1}";
                return false;
            }

            int copyLength = Math.Min(encoded.Length, LevelNameBufferSize - 1);
            Array.Copy(encoded, bytes, copyLength);

            Trace.WriteLine($"[LevelNameCodeInjector] WriteLevelName map='{levelName}' utf8Length={encoded.Length} storedLength={copyLength} hex={ToHex(bytes, copyLength)}");

            return WriteBytes(levelNameAddress, bytes, out error);
        }

        private static string BuildLoadMapCommand(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
            {
                return LoadMapPrefix.TrimEnd();
            }

            return LoadMapPrefix + levelName;
        }

        private bool ReadHitFlag(out int value, out string error)
        {
            value = 0;
            error = null;

            if (hitFlagAddress == IntPtr.Zero)
            {
                error = "Hit flag buffer is not initialized.";
                return false;
            }

            if (!NativeMethods.ReadProcessMemory(process.Handle, hitFlagAddress, out var bytes, HitFlagSize, out var read) || read != HitFlagSize)
            {
                error = "Failed to read hit flag.";
                return false;
            }

            value = BitConverter.ToInt32(bytes, 0);
            return true;
        }

        private bool WriteHitFlag(int value, out string error)
        {
            error = null;

            if (hitFlagAddress == IntPtr.Zero)
            {
                error = "Hit flag buffer is not initialized.";
                return false;
            }

            var bytes = BitConverter.GetBytes(value);
            return WriteBytes(hitFlagAddress, bytes, out error);
        }

        private bool ReadHitCount(out int value, out string error)
        {
            value = 0;
            error = null;

            if (hitCountAddress == IntPtr.Zero)
            {
                error = "Hit count buffer is not initialized.";
                return false;
            }

            if (!NativeMethods.ReadProcessMemory(process.Handle, hitCountAddress, out var bytes, HitCountSize, out var read) || read != HitCountSize)
            {
                error = "Failed to read hit count.";
                return false;
            }

            value = BitConverter.ToInt32(bytes, 0);
            return true;
        }

        private bool WriteHitCount(int value, out string error)
        {
            error = null;

            if (hitCountAddress == IntPtr.Zero)
            {
                error = "Hit count buffer is not initialized.";
                return false;
            }

            var bytes = BitConverter.GetBytes(value);
            return WriteBytes(hitCountAddress, bytes, out error);
        }

        private bool ReadCapturedSource(out string value, out string error)
        {
            value = null;
            error = null;

            if (capturedSourceAddress == IntPtr.Zero)
            {
                error = "Captured source buffer is not initialized.";
                return false;
            }

            if (!NativeMethods.ReadProcessMemory(process.Handle, capturedSourceAddress, out var bytes, CapturedSourceBufferSize, out var read) || read != CapturedSourceBufferSize)
            {
                error = "Failed to read captured source.";
                return false;
            }

            int zeroIndex = Array.IndexOf(bytes, (byte)0);
            int length = zeroIndex >= 0 ? zeroIndex : bytes.Length;
            value = Encoding.UTF8.GetString(bytes, 0, length);
            return true;
        }

        private bool WriteCapturedSource(string value, out string error)
        {
            error = null;

            if (capturedSourceAddress == IntPtr.Zero)
            {
                error = "Captured source buffer is not initialized.";
                return false;
            }

            if (value == null)
            {
                value = string.Empty;
            }

            var bytes = new byte[CapturedSourceBufferSize];
            var encoded = Encoding.UTF8.GetBytes(value);
            int copyLength = Math.Min(encoded.Length, CapturedSourceBufferSize - 1);
            Array.Copy(encoded, bytes, copyLength);
            return WriteBytes(capturedSourceAddress, bytes, out error);
        }

        private bool TryFindUniqueInjectionAddress(out IntPtr address, out IntPtr moduleBase, out string error)
        {
            address = IntPtr.Zero;
            moduleBase = IntPtr.Zero;
            error = null;

            Module64 mainModule = null;
            try
            {
                mainModule = process.Modules64()?.FirstOrDefault(m =>
                    string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                mainModule = null;
            }

            if (mainModule == null)
            {
                error = $"Target module '{moduleName}' was not found.";
                return false;
            }

            moduleBase = mainModule.BaseAddress;

            var searcher = new MemorySearcher();
            var allMatches = searcher.FindSignatures(process, Signature);
            if (allMatches == null || allMatches.Count == 0)
            {
                error = "AOB signature was not found.";
                return false;
            }

            long begin = mainModule.BaseAddress.ToInt64();
            long end = begin + mainModule.MemorySize;
            var inModule = allMatches.Where(p => p.ToInt64() >= begin && p.ToInt64() < end).ToList();

            if (inModule.Count != 1)
            {
                error = $"AOB signature must be unique in module. Found: {inModule.Count}";
                return false;
            }

            address = inModule[0];
            return true;
        }

        private IntPtr AllocateNear(IntPtr targetAddress, int size)
        {
            const long step = 0x10000;
            long target = targetAddress.ToInt64();

            for (long offset = 0; offset < MaxRel32Distance; offset += step)
            {
                long up = target + offset;
                if (up > 0)
                {
                    var allocated = NativeMethods.VirtualAllocEx(
                        process.Handle,
                        new IntPtr(up),
                        new IntPtr(size),
                        NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                        NativeMethods.PAGE_EXECUTE_READWRITE);

                    if (allocated != IntPtr.Zero)
                    {
                        if (TryGetRel32(targetAddress, allocated, 5, out _))
                        {
                            return allocated;
                        }

                        NativeMethods.VirtualFreeEx(process.Handle, allocated, IntPtr.Zero, NativeMethods.MEM_RELEASE);
                    }
                }

                if (offset == 0) continue;

                long down = target - offset;
                if (down > 0)
                {
                    var allocated = NativeMethods.VirtualAllocEx(
                        process.Handle,
                        new IntPtr(down),
                        new IntPtr(size),
                        NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                        NativeMethods.PAGE_EXECUTE_READWRITE);

                    if (allocated != IntPtr.Zero)
                    {
                        if (TryGetRel32(targetAddress, allocated, 5, out _))
                        {
                            return allocated;
                        }

                        NativeMethods.VirtualFreeEx(process.Handle, allocated, IntPtr.Zero, NativeMethods.MEM_RELEASE);
                    }
                }
            }

            var fallback = NativeMethods.VirtualAllocEx(
                process.Handle,
                IntPtr.Zero,
                new IntPtr(size),
                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                NativeMethods.PAGE_EXECUTE_READWRITE);

            if (fallback != IntPtr.Zero && TryGetRel32(targetAddress, fallback, 5, out _))
            {
                return fallback;
            }

            if (fallback != IntPtr.Zero)
            {
                NativeMethods.VirtualFreeEx(process.Handle, fallback, IntPtr.Zero, NativeMethods.MEM_RELEASE);
            }

            return IntPtr.Zero;
        }

        private bool ReadBytes(IntPtr address, int length, out byte[] bytes, out string error)
        {
            bytes = new byte[length];
            error = null;

            if (!NativeMethods.ReadProcessMemory(process.Handle, address, bytes, length, out var read) || read != length)
            {
                error = "Failed to read original bytes at injection point.";
                bytes = null;
                return false;
            }

            return true;
        }

        private bool WriteBytes(IntPtr address, byte[] bytes, out string error)
        {
            error = null;
            if (bytes == null || bytes.Length == 0)
            {
                return true;
            }

            if (!NativeMethods.VirtualProtectEx(process.Handle, address, new IntPtr(bytes.Length), NativeMethods.PAGE_EXECUTE_READWRITE, out var oldProtect))
            {
                error = "VirtualProtectEx failed before writing.";
                return false;
            }

            bool success = false;
            try
            {
                success = NativeMethods.WriteProcessMemory(process.Handle, address, bytes, bytes.Length, out var written) && written == bytes.Length;
                if (!success)
                {
                    error = "WriteProcessMemory failed.";
                    return false;
                }

                NativeMethods.FlushInstructionCache(process.Handle, address, new IntPtr(bytes.Length));
                return true;
            }
            finally
            {
                NativeMethods.VirtualProtectEx(process.Handle, address, new IntPtr(bytes.Length), oldProtect, out _);
            }
        }

        private static bool TryGetRel32(IntPtr fromInstructionAddress, IntPtr toAddress, int instructionSize, out int rel)
        {
            long fromNext = fromInstructionAddress.ToInt64() + instructionSize;
            long diff = toAddress.ToInt64() - fromNext;
            if (diff < int.MinValue || diff > int.MaxValue)
            {
                rel = 0;
                return false;
            }

            rel = (int)diff;
            return true;
        }

        private static void AppendMovRaxFromAbsolute(List<byte> code, ulong address)
        {
            code.Add(0x48);
            code.Add(0xA1);
            code.AddRange(BitConverter.GetBytes(address));
        }

        private static void AppendMovRaxImmediate(List<byte> code, ulong value)
        {
            code.Add(0x48);
            code.Add(0xB8);
            code.AddRange(BitConverter.GetBytes(value));
        }

        private static void AppendMovAbsoluteFromRax(List<byte> code, ulong address)
        {
            code.Add(0x48);
            code.Add(0xA3);
            code.AddRange(BitConverter.GetBytes(address));
        }

        private static void WriteInt32(List<byte> code, int index, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            code[index + 0] = bytes[0];
            code[index + 1] = bytes[1];
            code[index + 2] = bytes[2];
            code[index + 3] = bytes[3];
        }

        private static string ToHex(byte[] bytes, int length)
        {
            if (bytes == null || length <= 0) return string.Empty;

            int actualLength = Math.Min(length, bytes.Length);
            var sb = new StringBuilder(actualLength * 3);
            for (int i = 0; i < actualLength; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2"));
            }

            return sb.ToString();
        }

        private static ulong LoadMapPrefixQword => BitConverter.ToUInt64(Encoding.ASCII.GetBytes(LoadMapPrefix), 0);

        private static class NativeMethods
        {
            public const uint MEM_COMMIT = 0x1000;
            public const uint MEM_RESERVE = 0x2000;
            public const uint MEM_RELEASE = 0x8000;

            public const uint PAGE_READWRITE = 0x04;
            public const uint PAGE_EXECUTE_READWRITE = 0x40;

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint dwFreeType);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

            public static bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead)
            {
                lpBuffer = new byte[dwSize];
                return ReadProcessMemory(hProcess, lpBaseAddress, lpBuffer, dwSize, out lpNumberOfBytesRead);
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr dwSize);
        }
    }
}