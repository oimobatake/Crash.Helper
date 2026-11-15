using System;
using System.Collections.Generic;

namespace Crash.Helper.Memory.Runtime
{
    internal sealed class RuntimeCode
    {
        private readonly long start;
        private readonly List<byte> bytes = new List<byte>();
        private readonly Dictionary<string, int> labels = new Dictionary<string, int>();
        private readonly List<Tuple<int, string>> branches = new List<Tuple<int, string>>();
        internal RuntimeCode(long start) { this.start = start; }
        internal long Address => start + bytes.Count;
        internal void Emit(params byte[] value) => bytes.AddRange(value);
        internal void Int(int value) => Emit(BitConverter.GetBytes(value));
        internal void Long(long value) => Emit(BitConverter.GetBytes(value));
        internal void Label(string name) => labels.Add(name, bytes.Count);
        internal void Branch(string name, params byte[] opcode) { Emit(opcode); branches.Add(Tuple.Create(bytes.Count, name)); Int(0); }
        internal void Rip(long target, params byte[] opcode) { Emit(opcode); Int(checked((int)(target - Address - 4))); }
        internal void RipImmediate(long target, int value, params byte[] opcode) { Emit(opcode); Int(checked((int)(target - Address - 8))); Int(value); }
        internal void Data(long offset, params byte[] opcode)
        {
            var instruction = (byte[])opcode.Clone(); instruction[instruction.Length - 1] = (byte)((instruction[instruction.Length - 1] & 0x38) | 0x83);
            Emit(instruction); Int(checked((int)offset));
        }
        internal void DataImmediate(long offset, int value, params byte[] opcode) { Data(offset, opcode); Int(value); }
        internal void Call(long target) { Emit(0x48, 0xB8); Long(target); Emit(0xFF, 0xD0); }
        internal void Save(bool aligned = false)
        {
            Emit(0x9C, 0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53);
            Emit(0x48, 0x81, 0xEC, aligned ? (byte)0xA0 : (byte)0xA8, 0, 0, 0);
            for (int i = 0; i < 6; i++) Emit(0xF3, 0x0F, 0x7F, (byte)(0x44 + i * 8), 0x24, (byte)(0x20 + i * 16));
        }
        internal void Restore(bool aligned = false)
        {
            for (int i = 0; i < 6; i++) Emit(0xF3, 0x0F, 0x6F, (byte)(0x44 + i * 8), 0x24, (byte)(0x20 + i * 16));
            Emit(0x48, 0x81, 0xC4, aligned ? (byte)0xA0 : (byte)0xA8, 0, 0, 0, 0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58, 0x9D);
        }
        internal void Guards(long data, string failure, bool dynamic = false)
        {
            if (dynamic) { Data(12, 0x44, 0x8B, 0x15); Data(0x100, 0x4C, 0x8D, 0x0D); }
            else { Rip(data + 12, 0x44, 0x8B, 0x15); Rip(data + 0x100, 0x4C, 0x8D, 0x0D); }
            Label("guard"); Emit(0x45, 0x85, 0xD2); Branch("run", 0x0F, 0x84);
            Emit(0x49, 0x8B, 0x01, 0x4D, 0x8B, 0x59, 8, 0x41, 0x8B, 0x49, 16);
            Emit(0x83, 0xF9, 8); Branch("guard64", 0x0F, 0x84);
            Emit(0x83, 0xF9, 1); Branch("guardByte", 0x0F, 0x84);
            Emit(0x8B, 0, 0x41, 0x23, 0x41, 20, 0x44, 0x39, 0xD8); Branch("compared", 0xE9);
            Label("guardByte"); Emit(0x44, 0x38, 0x18); Branch("compared", 0xE9);
            Label("guard64"); Emit(0x4C, 0x39, 0x18);
            Label("compared"); Branch(failure, 0x0F, 0x85);
            Emit(0x49, 0x83, 0xC1, 24, 0x41, 0xFF, 0xCA); Branch("guard", 0xE9);
            Label("run");
        }
        internal byte[] Finish()
        {
            var result = bytes.ToArray();
            foreach (var branch in branches) BitConverter.GetBytes(labels[branch.Item2] - branch.Item1 - 4).CopyTo(result, branch.Item1);
            if (result.Length > 4096) throw new InvalidOperationException("The transform dispatcher exceeds its code page.");
            return result;
        }
        internal static byte[] Jump(long from, long to, int size = 5)
        {
            var result = new byte[size]; for (int i = 0; i < size; i++) result[i] = 0x90;
            result[0] = 0xE9; BitConverter.GetBytes(checked((int)(to - from - 5))).CopyTo(result, 1); return result;
        }
        internal static void PackGuards(byte[] bytes, IList<RuntimeGuard> guards)
        {
            if (guards.Count > (bytes.Length - 0x100) / 24) throw new InvalidOperationException("Too many engine transform guards.");
            BitConverter.GetBytes(guards.Count).CopyTo(bytes, 12);
            for (int i = 0; i < guards.Count; i++)
            {
                var guard = guards[i]; int offset = 0x100 + i * 24;
                BitConverter.GetBytes(guard.Address).CopyTo(bytes, offset); BitConverter.GetBytes(guard.Value).CopyTo(bytes, offset + 8);
                BitConverter.GetBytes(guard.Size).CopyTo(bytes, offset + 16); BitConverter.GetBytes(guard.Mask).CopyTo(bytes, offset + 20);
            }
        }
        internal static int SelectSlot(long first, long current, long inUse)
        {
            for (int i = 0; i < 3; i++) if (first + i * 4096L != current && first + i * 4096L != inUse) return i;
            throw new InvalidOperationException("No inactive transform command slot is available.");
        }
    }
}
