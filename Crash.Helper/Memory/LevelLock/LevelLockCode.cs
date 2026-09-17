using System;
using System.Collections.Generic;
using System.Text;

namespace Crash.Helper.Memory.LevelLock
{
    internal static class LevelLockCode
    {
        internal static readonly byte[] Original = { 0x41, 0x88, 0x00, 0x48, 0x85, 0xD2 };

        internal static byte[] EncodeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOf('\0') >= 0) throw new ArgumentException("A level name is required.");
            var encoded = Encoding.UTF8.GetBytes(name);
            if (encoded.Length >= 64) throw new ArgumentException("The level name must fit in 63 UTF-8 bytes.");
            var buffer = new byte[64];
            Buffer.BlockCopy(encoded, 0, buffer, 0, encoded.Length);
            return buffer;
        }

        internal static byte[] Jump(long from, long to, bool pad)
        {
            long distance = to - (from + 5);
            if (distance < int.MinValue || distance > int.MaxValue) throw new InvalidOperationException("The level hook is outside the relative jump range.");
            var code = new List<byte> { 0xE9 };
            code.AddRange(BitConverter.GetBytes((int)distance));
            if (pad) code.Add(0x90);
            return code.ToArray();
        }

        internal static byte[] Build(long address, long mapRoot, long control, long returnAddress, out int originalOffset)
        {
            var code = new List<byte>();
            var skipBranches = new List<int>();
            Action<byte[]> emit = bytes => code.AddRange(bytes);
            Action<byte> skip = condition => { emit(new byte[] { 0x0F, condition }); skipBranches.Add(code.Count); emit(new byte[4]); };
            emit(new byte[] { 0x50, 0x41, 0x52 }); // push rax; push r10
            emit(new byte[] { 0x49, 0xBA }); emit(BitConverter.GetBytes(control)); // mov r10, control
            emit(new byte[] { 0x41, 0x80, 0x3A, 0x00 }); skip(0x84); // cmp byte [r10], 0; je original
            emit(new byte[] { 0x48, 0xB8 }); emit(BitConverter.GetBytes(mapRoot)); // mov rax, map root address
            emit(new byte[] { 0x48, 0x8B, 0x00, 0x48, 0x85, 0xC0 }); skip(0x84); // mov rax,[rax]; test rax,rax
            emit(new byte[] { 0x48, 0x83, 0xC0, 0x18, 0x49, 0x39, 0xC0 }); skip(0x85); // add rax,18; cmp r8,rax
            emit(new byte[] { 0x48, 0x85, 0xD2 }); skip(0x84); // test rdx,rdx
            emit(new byte[] { 0x4D, 0x8B, 0x52, 0x08 }); // mov r10,[r10+8]: immutable name snapshot
            for (byte offset = 0; offset < 64; offset += 8)
            {
                emit(new byte[] { 0x49, 0x8B, 0x42, offset }); // mov rax,[r10+offset]
                emit(new byte[] { 0x48, 0x89, 0x42, offset }); // mov [rdx+offset],rax
            }
            int restoreOffset = code.Count;
            emit(new byte[] { 0x41, 0x5A, 0x58 }); // pop r10; pop rax
            originalOffset = code.Count;
            emit(Original);
            emit(Jump(address + code.Count, returnAddress, false));
            foreach (int branch in skipBranches)
            {
                byte[] distance = BitConverter.GetBytes(restoreOffset - (branch + 4));
                for (int i = 0; i < 4; i++) code[branch + i] = distance[i];
            }
            return code.ToArray();
        }
    }
}
