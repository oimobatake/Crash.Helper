using System;
using System.Diagnostics;
using System.Linq;
using Crash.Helper.Memory.Runtime;

namespace Crash.Helper.Memory.Position
{
    // Execute the actor's own teleport on the game thread, after checking its complete ownership chain.
    internal sealed class PlayerPositionPatch : IDisposable
    {
        private readonly RuntimeMemory memory;
        private readonly long hook, cave, data;
        private readonly byte[] jump;
        private static readonly byte[] Original = { 0x48, 0x89, 0x5C, 0x24, 8 };
        private bool installed, pending;
        private PlayerBinding submitted;
        private float[] lastPosition;
        private PlayerBinding lastBinding;
        private PositionWriteFilter writeFilter;
        private readonly int thread;
        internal bool Pending => pending;
        internal long SubmittedCommand { get; private set; }
        internal long CompletedCommand { get; private set; }
        internal bool CommandSucceeded { get; private set; }

        internal PlayerPositionPatch(RuntimeMemory memory)
        {
            this.memory = memory; hook = memory.Base + 0xC003B0;
            memory.Require(hook, Original);
            memory.Require(memory.Base + 0x508EA0, new byte[] { 0x48, 0x89, 0x5C, 0x24, 8, 0x57, 0x48, 0x81, 0xEC, 0x80, 0, 0, 0 });
            thread = memory.Process.Threads.Cast<ProcessThread>().OrderBy(t => t.StartTime).First().Id;
            cave = memory.Allocate(hook, 12288); data = cave + 4096; jump = RuntimeCode.Jump(hook, cave);
            memory.Write(cave, Build(cave, data, memory.Base, thread)); memory.Protect(cave);
            memory.WhilePaused(() =>
            {
                memory.Require(hook, Original);
                try { memory.WriteCode(hook, jump); installed = true; }
                catch { memory.WriteCode(hook, Original); throw; }
            });
        }
        internal static byte[] Build(long cave, long data, long module, int thread)
        {
            var c = new RuntimeCode(cave); c.Save();
            c.Emit(0x65, 0x81, 0x3C, 0x25, 0x48, 0, 0, 0); c.Int(thread); c.Branch("restore", 0x0F, 0x85);
            c.Emit(0xB8, 1, 0, 0, 0, 0xB9, 2, 0, 0, 0); c.Rip(data, 0xF0, 0x0F, 0xB1, 0x0D); c.Branch("restore", 0x0F, 0x85);
            c.Guards(data, "changed");
            c.Rip(data + 16, 0x48, 0x8B, 0x0D); c.Rip(data + 48, 0x48, 0x8D, 0x15); c.Call(module + 0xC003B0);
            c.Rip(data + 8, 0x8B, 0x05); c.Emit(0x85, 0xC0); c.Branch("success", 0x0F, 0x84);
            for (int axis = 0; axis < 3; axis++)
            {
                c.RipImmediate(data + 8, 1 << axis, 0xF7, 0x05); c.Branch("axis" + axis, 0x0F, 0x84);
                c.Rip(data + 32 + axis * 4, 0xF3, 0x0F, 0x10, 0x05);
                c.Rip(data + 48 + axis * 4, 0xF3, 0x0F, 0x5C, 0x05);
                c.Rip(data + 80 + axis * 4, 0xF3, 0x0F, 0x11, 0x05);
                c.Rip(data + 32 + axis * 4, 0x8B, 0x05); c.Rip(data + 48 + axis * 4, 0x89, 0x05);
                c.Label("axis" + axis);
            }
            c.Rip(data + 16, 0x48, 0x8B, 0x05); c.Rip(data + 24, 0x48, 0x3B, 0x05); c.Branch("hero", 0x0F, 0x84);
            c.Rip(data + 24, 0x48, 0x8B, 0x0D); c.Rip(data + 64, 0x48, 0x8D, 0x15); c.Call(module + 0xC003B0);
            for (int axis = 0; axis < 3; axis++)
            {
                c.Rip(data + 64 + axis * 4, 0xF3, 0x0F, 0x10, 0x05); c.Rip(data + 80 + axis * 4, 0xF3, 0x0F, 0x58, 0x05);
                c.Rip(data + 64 + axis * 4, 0xF3, 0x0F, 0x11, 0x05);
            }
            c.Rip(data + 24, 0x48, 0x8B, 0x0D); c.Rip(data + 64, 0x48, 0x8D, 0x15); c.Call(module + 0x508EA0);
            c.Label("hero"); c.Rip(data + 16, 0x48, 0x8B, 0x0D); c.Rip(data + 48, 0x48, 0x8D, 0x15); c.Call(module + 0x508EA0);
            c.Label("success"); c.RipImmediate(data + 4, 1, 0xC7, 0x05); c.Branch("complete", 0xE9);
            c.Label("changed"); c.RipImmediate(data + 4, 2, 0xC7, 0x05);
            c.Label("complete"); c.RipImmediate(data, 0, 0xC7, 0x05);
            c.Label("restore"); c.Restore(); c.Emit(Original); c.Emit(RuntimeCode.Jump(c.Address, module + 0xC003B5));
            return c.Finish();
        }
        internal void Poll()
        {
            if (!pending || memory.Number(data, 4) != 0) return;
            CompletedCommand = SubmittedCommand; CommandSucceeded = memory.Number(data + 4, 4) == 1;
            if (CommandSucceeded)
            {
                var bytes = memory.Read(data + 48, 12);
                lastPosition = Enumerable.Range(0, 3).Select(i => BitConverter.ToSingle(bytes, i * 4)).ToArray(); lastBinding = submitted;
            }
            pending = false;
        }
        internal float[] Read(PlayerBinding binding)
        {
            Poll();
            return memory.UnattachedPosition(binding) ?? (binding.SameActor(lastBinding) && lastPosition != null && lastPosition.All(RuntimeMemory.Finite) ? (float[])lastPosition.Clone() : null);
        }
        internal bool Submit(PlayerBinding binding, float?[] values)
        {
            Poll(); if (pending || binding == null || (values != null && !binding.Writable) || !memory.Matches(binding.Guards)) return false;
            var bytes = new byte[8192]; int mask = 0;
            if (values != null) for (int i = 0; i < 3; i++) if (values[i].HasValue)
            {
                float value = values[i].Value;
                if (!RuntimeMemory.Finite(value)) throw new ArgumentOutOfRangeException(nameof(values));
                mask |= 1 << i; BitConverter.GetBytes(value).CopyTo(bytes, 32 + i * 4);
            }
            BitConverter.GetBytes(mask).CopyTo(bytes, 8); BitConverter.GetBytes(binding.Actor).CopyTo(bytes, 16); BitConverter.GetBytes(binding.Driver).CopyTo(bytes, 24);
            RuntimeCode.PackGuards(bytes, binding.Guards); memory.Require(hook, jump); memory.Write(data, bytes);
            submitted = binding; SubmittedCommand++; pending = true; memory.Write(data, BitConverter.GetBytes(1)); return true;
        }
        internal bool Cancel()
        {
            writeFilter?.Clear();
            Poll(); lastBinding = null; lastPosition = null;
            if (!pending) return true;
            memory.WhilePaused(() => { if (memory.Number(data, 4) == 1) { memory.Write(data, BitConverter.GetBytes(0)); pending = false; } });
            Poll(); return !pending;
        }
        internal void ConfigureFreeze(PlayerBinding binding, bool[] axes)
        {
            if (writeFilter == null && binding != null && binding.Writable && axes.Any(value => value))
                writeFilter = new PositionWriteFilter(memory, data, thread);
            writeFilter?.Configure(binding, axes);
        }
        public void Dispose()
        {
            if (!installed || memory.Process.HasExited) return;
            Cancel();
            writeFilter?.Dispose(); writeFilter = null;
            memory.WhilePaused(() => { memory.Require(hook, jump); memory.WriteCode(hook, Original); installed = false; });
            // Keep this immutable code page until process exit: a thread may still be in its epilogue.
        }
    }
}
