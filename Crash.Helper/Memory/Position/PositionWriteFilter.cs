using System;
using System.Collections.Generic;
using System.Linq;
using Crash.Helper.Memory.Runtime;

namespace Crash.Helper.Memory.Position
{
    // Reject game writes at both entity-position stores; helper teleports have a scoped bypass.
    internal sealed class PositionWriteFilter : IDisposable
    {
        private readonly RuntimeMemory memory;
        private readonly long allocation, header;
        private readonly byte[][] jumps = new byte[2][];
        private byte[] published;
        private bool installed;
        internal static readonly int[] Hooks = { 0xC0582A, 0xC05B6D };
        internal static readonly byte[][] Original = {
            new byte[] { 0xF2, 0x0F, 0x10, 2, 0xF2, 0x0F, 0x11, 0x41, 0x20, 0x8B, 0x42, 8, 0x89, 0x41, 0x28 },
            new byte[] { 0x8B, 0x44, 0x24, 0x28, 0xF2, 0x0F, 0x10, 0x44, 0x24, 0x20, 0xF2, 0x0F, 0x11, 0x43, 0x20, 0x89, 0x43, 0x28 }
        };

        internal PositionWriteFilter(RuntimeMemory memory, long dispatcher, int thread)
        {
            this.memory = memory;
            for (int i = 0; i < 2; i++) memory.Require(memory.Base + Hooks[i], Original[i]);
            allocation = memory.Allocate(memory.Base + Hooks[0], 24576); header = allocation + 8192;
            for (int i = 0; i < 2; i++)
            {
                long code = allocation + i * 4096L;
                memory.Write(code, Build(code, header, memory.Base, dispatcher, thread, i)); memory.Protect(code);
                jumps[i] = RuntimeCode.Jump(memory.Base + Hooks[i], code, Original[i].Length);
            }
            memory.WhilePaused(() =>
            {
                for (int i = 0; i < 2; i++) memory.Require(memory.Base + Hooks[i], Original[i]);
                // These store blocks have no relative instructions. Preserve in-flight boundaries.
                Crash.Helper.Memory.LevelLock.LevelLockNative.RelocateThreads(memory.Process, rip =>
                {
                    for (int i = 0; i < 2; i++)
                    {
                        long offset = rip - memory.Base - Hooks[i];
                        if (offset > 0 && offset < Original[i].Length) return allocation + i * 4096L + 0xF00 + offset;
                    }
                    return rip;
                });
                int count = 0;
                try { for (; count < 2; count++) memory.WriteCode(memory.Base + Hooks[count], jumps[count]); installed = true; }
                catch { for (int i = 0; i <= count && i < 2; i++) memory.WriteCode(memory.Base + Hooks[i], Original[i]); throw; }
            });
        }

        internal static byte[] Build(long cave, long header, long module, long dispatcher, int thread, int kind)
        {
            var c = new RuntimeCode(cave); c.Save(); c.Emit(0x53);
            // Only the helper's main-thread teleport may bypass the writer filter.
            c.RipImmediate(dispatcher, 2, 0x81, 0x3D); c.Branch("claim", 0x0F, 0x85);
            c.Emit(0x65, 0x81, 0x3C, 0x25, 0x48, 0, 0, 0); c.Int(thread); c.Branch("fallback", 0x0F, 0x84);
            c.Label("claim");
            c.Rip(header, 0x48, 0x8B, 0x1D); c.Emit(0x48, 0x85, 0xDB); c.Branch("fallback", 0x0F, 0x84);
            c.Emit(0x31, 0xC0); c.Rip(header + 8, 0xF0, 0x48, 0x0F, 0xB1, 0x1D); c.Branch("busy", 0x0F, 0x85);
            c.Rip(header, 0x48, 0x3B, 0x1D); c.Branch("retry", 0x0F, 0x85);
            if (kind == 0) c.Emit(0x49, 0x89, 0xC8); // R8 = entity; RDX = source XYZ.
            else c.Emit(0x4C, 0x8B, 0x84, 0x24, 0, 0, 0, 0, 0x48, 0x8D, 0x94, 0x24, 0x10, 1, 0, 0);
            // The saved RBX at [RSP] is the world setter's entity.
            c.Data(16, 0x4C, 0x3B, 0x05); c.Branch("entity", 0x0F, 0x84);
            c.Data(24, 0x4C, 0x3B, 0x05); c.Branch("releaseFallback", 0x0F, 0x85);
            c.Label("entity");
            // Attached heroes keep their local animation; their unattached movement owner is filtered.
            c.Emit(0x49, 0x83, 0x78, 0x10, 0); c.Branch("releaseFallback", 0x0F, 0x85);
            c.Guards(0, "releaseFallback", true);
            for (int axis = 0; axis < 3; axis++)
            {
                c.DataImmediate(8, 1 << axis, 0xF7, 0x05); c.Branch("axis" + axis, 0x0F, 0x85);
                c.Emit(0x8B, 0x42, (byte)(axis * 4), 0x41, 0x89, 0x40, (byte)(0x20 + axis * 4));
                c.Label("axis" + axis);
            }
            c.Rip(header + 16 + kind * 8, 0xFF, 0x05);
            c.Emit(0x31, 0xC0); c.Rip(header + 8, 0x48, 0x89, 0x05);
            c.Emit(0x5B); c.Restore();
            if (kind == 0) c.Emit(0xF2, 0x0F, 0x10, 2, 0x8B, 0x42, 8);
            else c.Emit(0x8B, 0x44, 0x24, 0x28, 0xF2, 0x0F, 0x10, 0x44, 0x24, 0x20);
            c.Emit(RuntimeCode.Jump(c.Address, module + Hooks[kind] + Original[kind].Length));
            c.Label("retry"); c.Emit(0x31, 0xC0); c.Rip(header + 8, 0x48, 0x89, 0x05); c.Branch("claim", 0xE9);
            c.Label("busy"); c.Emit(0xF3, 0x90); c.Branch("claim", 0xE9);
            c.Label("releaseFallback"); c.Emit(0x31, 0xC0); c.Rip(header + 8, 0x48, 0x89, 0x05);
            c.Label("fallback"); c.Emit(0x5B); c.Restore(); c.Emit(Original[kind]);
            c.Emit(RuntimeCode.Jump(c.Address, module + Hooks[kind] + Original[kind].Length));
            var result = c.Finish(); Array.Resize(ref result, 4096);
            Original[kind].CopyTo(result, 0xF00);
            RuntimeCode.Jump(cave + 0xF00 + Original[kind].Length, module + Hooks[kind] + Original[kind].Length).CopyTo(result, 0xF00 + Original[kind].Length);
            return result;
        }

        internal void Configure(PlayerBinding binding, bool[] axes)
        {
            int mask = 0; for (int i = 0; i < 3; i++) if (axes[i]) mask |= 1 << i;
            if (binding == null || !binding.Writable || mask == 0) { Clear(); return; }
            var bytes = new byte[4096]; BitConverter.GetBytes(mask).CopyTo(bytes, 8);
            BitConverter.GetBytes(binding.Actor).CopyTo(bytes, 16); BitConverter.GetBytes(binding.Driver).CopyTo(bytes, 24);
            RuntimeCode.PackGuards(bytes, binding.Guards);
            if (published != null && published.SequenceEqual(bytes)) return;
            if (!memory.Matches(binding.Guards)) { Clear(); return; }
            long first = header + 4096;
            long slot = first + RuntimeCode.SelectSlot(first, memory.Number(header), memory.Number(header + 8)) * 4096L;
            memory.Write(slot, bytes); memory.Write(header, BitConverter.GetBytes(slot)); published = bytes;
        }
        internal void Clear() { if (published == null) return; memory.Write(header, new byte[8]); published = null; }
        public void Dispose()
        {
            if (!installed || memory.Process.HasExited) return;
            Clear();
            memory.WhilePaused(() =>
            {
                for (int i = 0; i < 2; i++) memory.Require(memory.Base + Hooks[i], jumps[i]);
                Crash.Helper.Memory.LevelLock.LevelLockNative.RelocateThreads(memory.Process, rip =>
                {
                    for (int i = 0; i < 2; i++)
                        if (rip > memory.Base + Hooks[i] && rip < memory.Base + Hooks[i] + Original[i].Length)
                            return memory.Base + Hooks[i] + Original[i].Length;
                    return rip;
                });
                for (int i = 0; i < 2; i++) memory.WriteCode(memory.Base + Hooks[i], Original[i]);
                installed = false;
            });
        }
    }
}
