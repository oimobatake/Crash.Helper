using System;
using System.Diagnostics;
using System.Linq;

namespace Crash.Helper.Memory.Masks
{
    internal sealed class MaskCommandPatch : IDisposable
    {
        private readonly MaskMemory memory;
        private readonly long hook, cave, data;
        private readonly byte[] jump;
        private bool installed;
        internal bool Pending { get; private set; }
        internal bool Claimed => Pending && memory.Number(data, 4) == 0;
        private long sentAt;

        internal MaskCommandPatch(MaskMemory memory)
        {
            this.memory = memory; hook = memory.Base + MaskCommandCode.HookOffset;
            memory.Require(hook, MaskCommandCode.Original);
            memory.Require(memory.Base + 0xDDC490, new byte[] { 0x48, 0x89, 0x5C, 0x24, 8, 0x57, 0x48, 0x83, 0xEC, 0x20 });
            memory.Require(memory.Base + 0xC988C0, new byte[] { 0x4C, 0x8B, 0xDC, 0x49, 0x89, 0x6B, 0x18, 0x56, 0x41, 0x56 });
            memory.Require(memory.Base + 0xCBC2A0, new byte[] { 0xC7, 0x41, 0x10, 0, 0, 0, 0, 0x48, 0x89, 0x11 });
            memory.Require(memory.Base + 0xC5EF20, new byte[] { 0x40, 0x53, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x8B, 0xD9 });
            memory.Require(memory.Base + 0xDF0E10, new byte[] { 0x48, 0x85, 0xC9, 0x0F, 0x85 });
            memory.Require(memory.Base + 0xCBC740, new byte[] { 0x40, 0x57, 0x48, 0x83, 0xEC, 0x20, 0x80, 0x79, 0x4D, 0 });
            int thread = memory.Process.Threads.Cast<ProcessThread>().OrderBy(t => t.StartTime).First().Id;
            long anchor = hook & ~65535L;
            for (long distance = 65536; cave == 0 && distance < 0x10000000; distance += 65536)
                foreach (long hint in new[] { anchor + distance, anchor - distance })
                {
                    cave = ProcessNative.VirtualAllocEx(memory.Handle, new IntPtr(hint), (UIntPtr)8192, 0x3000, 4).ToInt64();
                    if (cave != 0) break;
                }
            if (cave == 0) throw new InvalidOperationException("Could not allocate the mask dispatcher.");
            data = cave + 4096; jump = MaskCommandCode.Jump(hook, cave);
            Write(cave, MaskCommandCode.Build(cave, data, memory.Base, thread));
            uint previous;
            ProcessNative.Check(ProcessNative.VirtualProtectEx(memory.Handle, new IntPtr(cave), (UIntPtr)4096, 0x20, out previous), "Protect mask dispatcher");
            ProcessNative.Check(ProcessNative.FlushInstructionCache(memory.Handle, new IntPtr(cave), (UIntPtr)4096), "Flush mask dispatcher");
        }

        private void Install()
        {
            if (installed) return;
            ProcessNative.WhilePaused(memory.Handle, memory.Process, () =>
            {
                memory.Require(hook, MaskCommandCode.Original);
                // A five-byte MOV is replaced as a single instruction; no partial original boundary exists.
                try { WriteCode(hook, jump); installed = true; }
                catch { WriteCode(hook, MaskCommandCode.Original); throw; }
            });
        }

        private void Write(long address, byte[] bytes)
        {
            UIntPtr written;
            ProcessNative.Check(ProcessNative.WriteProcessMemory(memory.Handle, new IntPtr(address), bytes, (UIntPtr)bytes.Length, out written) && written.ToUInt64() == (ulong)bytes.Length, "Write mask dispatcher");
        }
        private void WriteCode(long address, byte[] bytes)
            => ProcessNative.WriteCode(memory.Handle, address, bytes, "mask");
        internal void Send(MaskSnapshot snapshot, int direction)
        {
            Submit(snapshot, direction > 0 ? 0 : 1, direction);
        }

        internal void Restore(MaskSnapshot snapshot) { Submit(snapshot, 2, 1); }
        private void Submit(MaskSnapshot snapshot, int action, int direction)
        {
            if (Pending) throw new InvalidOperationException("A mask event is already in flight.");
            var guards = memory.CommandGuards(snapshot).Concat(new[] { new MaskGuard(snapshot.InvincibleAddress, snapshot.Invincible ? 1 : 0, 1) }).ToArray();
            if (snapshot.Minimum < 0 || snapshot.Minimum > 3) throw new InvalidOperationException("Invalid mask assist minimum.");
            guards = guards.Concat(new[] { new MaskGuard(snapshot.Binding.MinimumAddress, snapshot.Minimum, 4) }).ToArray();
            if (action == 2) guards = guards.Concat(new[] { new MaskGuard(snapshot.Binding.RuntimeStateAddress, unchecked((uint)snapshot.RuntimeState), 4) }).ToArray();
            if (guards.Length > MaskCommandCode.GuardLimit) throw new InvalidOperationException("Too many mask command guards.");
            if (!memory.Matches(guards)) return;
            var bytes = new byte[4096];
            BitConverter.GetBytes(action).CopyTo(bytes, 8);
            BitConverter.GetBytes(guards.Length).CopyTo(bytes, 12);
            BitConverter.GetBytes(snapshot.Binding.Owner).CopyTo(bytes, 16);
            BitConverter.GetBytes(action == 0 ? snapshot.Binding.AddMethod : snapshot.Binding.RemoveMethod).CopyTo(bytes, 24);
            BitConverter.GetBytes(action == 0 ? snapshot.Binding.AddMeta : snapshot.Binding.RemoveMeta).CopyTo(bytes, 32);
            BitConverter.GetBytes(direction).CopyTo(bytes, 40);
            BitConverter.GetBytes(snapshot.Binding.RefreshMethod).CopyTo(bytes, 48);
            BitConverter.GetBytes(snapshot.Binding.RefreshMeta).CopyTo(bytes, 56);
            BitConverter.GetBytes(snapshot.Binding.EndMethod).CopyTo(bytes, 64);
            BitConverter.GetBytes(snapshot.Binding.EndMeta).CopyTo(bytes, 72);
            BitConverter.GetBytes(direction < 0 && snapshot.Invincible ? 1 : 0).CopyTo(bytes, 80);
            BitConverter.GetBytes(snapshot.Binding.InvincibilityTimer).CopyTo(bytes, 88);
            BitConverter.GetBytes(snapshot.Binding.SpawnMethod).CopyTo(bytes, 96);
            BitConverter.GetBytes(snapshot.Binding.SpawnMeta).CopyTo(bytes, 104);
            BitConverter.GetBytes(snapshot.Binding.ModelMethod).CopyTo(bytes, 112);
            BitConverter.GetBytes(snapshot.Binding.ModelMeta).CopyTo(bytes, 120);
            BitConverter.GetBytes(snapshot.Binding.RuntimeStateAddress).CopyTo(bytes, 128);
            BitConverter.GetBytes(snapshot.CountAddress).CopyTo(bytes, 136);
            BitConverter.GetBytes(snapshot.Binding.MinimumAddress).CopyTo(bytes, 144);
            BitConverter.GetBytes(snapshot.Minimum).CopyTo(bytes, 152);
            BitConverter.GetBytes(snapshot.Binding.TintOnMethod).CopyTo(bytes, 160);
            BitConverter.GetBytes(snapshot.Binding.TintOnMeta).CopyTo(bytes, 168);
            BitConverter.GetBytes(snapshot.Binding.TintOffMethod).CopyTo(bytes, 176);
            BitConverter.GetBytes(snapshot.Binding.TintOffMeta).CopyTo(bytes, 184);
            for (int i = 0; i < guards.Length; i++)
            {
                int offset = MaskCommandCode.GuardOffset + i * 24;
                BitConverter.GetBytes(guards[i].Address).CopyTo(bytes, offset);
                BitConverter.GetBytes(guards[i].Value).CopyTo(bytes, offset + 8);
                BitConverter.GetBytes(guards[i].Size).CopyTo(bytes, offset + 16);
            }
            Install();
            memory.Require(hook, jump); Write(data, bytes);
            sentAt = Stopwatch.GetTimestamp(); Pending = true;
            Write(data, BitConverter.GetBytes(1));
        }
        internal int Poll()
        {
            if (!Pending) return 0;
            int status = checked((int)memory.Number(data + 4, 4));
            if (status != 0) { Pending = false; return status; }
            if ((Stopwatch.GetTimestamp() - sentAt) / (double)Stopwatch.Frequency > 1 && !Claimed)
                return Cancel();
            return 0;
        }
        internal int Cancel()
        {
            if (!Pending) return 0;
            if (memory.Process.HasExited) { Pending = false; return 2; }
            int completion = 0;
            // Serialize cancellation with the game thread's atomic claim.
            ProcessNative.WhilePaused(memory.Handle, memory.Process, () =>
            {
                if (memory.Number(data, 4) != 0) { Write(data, BitConverter.GetBytes(0)); Pending = false; completion = 2; }
                else
                {
                    completion = checked((int)memory.Number(data + 4, 4));
                    if (completion != 0) Pending = false;
                }
            });
            return completion;
        }
        internal void RestoreWhenIdle()
        {
            if (!installed || Pending || memory.Process.HasExited) return;
            ProcessNative.WhilePaused(memory.Handle, memory.Process, () => { memory.Require(hook, jump); WriteCode(hook, MaskCommandCode.Original); installed = false; });
            // Reuse the block on the next event. An in-flight epilogue can still use its code.
        }
        public void Dispose()
        {
            Cancel();
            if (Pending) throw new InvalidOperationException("Wait for the active mask event to finish before removing its dispatcher.");
            RestoreWhenIdle();
        }
    }
}
