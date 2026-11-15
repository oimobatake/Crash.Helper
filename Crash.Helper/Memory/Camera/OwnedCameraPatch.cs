using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Crash.Helper.Memory.Runtime;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Memory.Camera
{
    // Every frame claims one immutable command slot. The host never edits an in-flight pose.
    internal sealed class OwnedCameraPatch : IDisposable
    {
        private readonly RuntimeMemory memory;
        private readonly long hook;
        private long cave, header, model, actor, epoch, returnRequest;
        private int groups;
        private bool installed, followSuspended, orbit, returnPending;
        private float scale = 1;
        private float[] desired, orbitPlayer;
        private float[] followOffset;
        private float followDeadzone, followSpeed;
        private long followGeneration;
        private long returnBox, returnCamera;
        private List<RuntimeGuard> returnGuards;
        private byte[] jump;
        private List<RuntimeGuard> binding;
        internal OwnedCameraPatch(RuntimeMemory memory) { this.memory = memory; hook = memory.Base + CameraFrameCode.Hook; }

        internal void Enable(bool xyz, bool rotation)
        {
            int next = (xyz ? 1 : 0) | (rotation ? 2 : 0);
            if (next != 0) EnsureInstalled();
            var current = ReadValues();
            if (groups == next) return;
            bool released = (groups & ~next) != 0;
            groups = next; orbit = false;
            if (next == 0) desired = null;
            else if (desired != null && current != null) desired = current;
            if (installed) Publish(released && !LoadingMemory.Read(memory.Process, GameMemoryProfile.Steam.Loading, memory.Base));
        }
        internal void ReturnToPlayer()
        {
            EnsureInstalled(); groups = 0; desired = null; orbit = false; followOffset = null; followSuspended = false;
            if (ReadValues() != null) Publish(true);
        }
        private void EnsureInstalled()
        {
            if (installed) return;
            memory.Require(hook, CameraFrameCode.Original);
            memory.Require(memory.Base + 0x500F10, new byte[] { 0x48, 0x89, 0x5C, 0x24 });
            memory.Require(memory.Base + 0x36DA30, new byte[] { 0x40, 0x53, 0x56, 0x57, 0x48, 0x83, 0xEC, 0x20 });
            memory.Require(memory.Base + 0x36B7B0, new byte[] { 0x48, 0x89, 0x7C, 0x24, 0x18, 0x41, 0x56 });
            if (cave == 0)
            {
                cave = memory.Allocate(hook, 20480); header = cave + 4096; jump = RuntimeCode.Jump(hook, cave, 8);
                memory.Write(cave, CameraFrameCode.Build(cave, header, memory.Base)); memory.Protect(cave);
            }
            memory.WhilePaused(() =>
            {
                memory.Require(hook, CameraFrameCode.Original);
                LevelLockNative.RelocateThreads(memory.Process, rip =>
                {
                    if (rip == hook + 3) return cave + 0xF00;
                    if (rip > hook && rip < hook + 8) throw new InvalidOperationException("Unexpected camera commit instruction boundary.");
                    return rip;
                });
                try { memory.WriteCode(hook, jump); installed = true; }
                catch { memory.WriteCode(hook, CameraFrameCode.Original); throw; }
            });
            Publish(false);
        }
        internal void SetFollowAvailable(bool available)
        {
            if (followSuspended == !available) return;
            followSuspended = !available; desired = null; orbit = false;
            if (installed) Publish(false);
        }
        internal void SetOrbit(bool enabled, float[] player, float followScale)
        {
            orbit = enabled && player != null && player.All(RuntimeMemory.Finite) && followScale > 0 && RuntimeMemory.Finite(followScale);
            orbitPlayer = orbit ? (float[])player.Clone() : null; scale = followScale;
        }
        internal void SetNativeFollow(CameraFollow follow)
        {
            followOffset = follow?.FollowOffset;
            if (follow == null) return;
            followDeadzone = follow.Deadzone; followSpeed = follow.Speed; followGeneration = follow.Generation;
        }
        internal float[] ReadValues()
        {
            try
            {
                var guards = new List<RuntimeGuard>(); long address = memory.Camera(guards);
                if (address == 0) { Invalidate(); return null; }
                var values = Pose(memory.Read(address, 24));
                if (!values.All(RuntimeMemory.Finite)) { Invalidate(); return null; }
                long subject = memory.CameraSubject(guards);
                if (!memory.Matches(guards)) return null;
                if (model != address || actor != subject || !SameGuards(binding, guards))
                {
                    model = address; actor = subject; binding = guards; desired = null; orbit = false; epoch++;
                    returnPending = false;
                    if (installed) Publish(false);
                }
                if (returnPending)
                {
                    if (memory.Number(header + CameraFrameCode.ReturnAcknowledgement) == returnRequest) { returnPending = false; Publish(false); }
                    else Publish(false);
                }
                if (actor == 0 || followSuspended) return values;
                if (desired == null && groups != 0)
                {
                    desired = AutomaticPose();
                    if (desired == null && LoadingMemory.Read(memory.Process, GameMemoryProfile.Steam.Camera.PauseMenu, memory.Base)) desired = (float[])values.Clone();
                    if (desired != null && installed) Publish(false);
                }
                if (desired != null)
                    for (int i = 0; i < 5; i++) if ((groups & (i < 3 ? 1 : 2)) != 0) values[i] = desired[i];
                if (followOffset != null && groups != 0)
                {
                    var applied = FramePose(96);
                    if (applied != null) for (int i = 0; i < 3; i++) values[i] = applied[i];
                }
                return values;
            }
            catch (Win32Exception) { Invalidate(); return null; }
            catch (InvalidOperationException) { Invalidate(); return null; }
            catch (OverflowException) { Invalidate(); return null; }
        }
        private float[] AutomaticPose() => FramePose(64);
        private float[] FramePose(int offset)
        {
            if (!installed) return null;
            long sequence = memory.Number(header + 16, 4);
            if (sequence == 0 || (sequence & 1) != 0 || memory.Number(header + 24) != epoch) return null;
            var result = Pose(memory.Read(header + offset, 24));
            return result.All(RuntimeMemory.Finite) && sequence == memory.Number(header + 16, 4) ? result : null;
        }
        private static float[] Pose(byte[] bytes) => new[] { BitConverter.ToSingle(bytes, 0), BitConverter.ToSingle(bytes, 4), BitConverter.ToSingle(bytes, 8), BitConverter.ToSingle(bytes, 20), BitConverter.ToSingle(bytes, 16) };
        private static bool SameGuards(List<RuntimeGuard> first, List<RuntimeGuard> second) => first != null && first.Count == second.Count && first.Zip(second, (a, b) => a.Address == b.Address && a.Value == b.Value && a.Size == b.Size && a.Mask == b.Mask).All(value => value);
        private void Invalidate()
        {
            model = actor = 0; binding = null; desired = null; orbit = false; epoch++;
            returnPending = false;
            if (installed && !memory.Process.HasExited) memory.Write(header, new byte[8]);
        }
        internal static int SelectSlot(long first, long current, long inUse)
            => RuntimeCode.SelectSlot(first, current, inUse);
        private void Publish(bool returnToPlayer)
        {
            if (!installed || binding == null) return;
            if (returnToPlayer) { returnRequest++; returnPending = true; returnGuards = null; }
            if (returnPending && memory.Number(header + CameraFrameCode.ReturnAcknowledgement) == returnRequest) returnPending = false;
            var guards = new List<RuntimeGuard>(binding); long nearest = 0, camera = 0;
            if (returnPending && actor != 0)
            {
                try
                {
                    if (returnGuards == null || !memory.Matches(returnGuards))
                    {
                        returnBox = memory.NearestCamera(model - 0x80, actor, guards, out returnCamera);
                        returnGuards = returnBox == 0 ? null : new List<RuntimeGuard>(guards);
                    }
                    if (returnGuards != null) { guards = new List<RuntimeGuard>(returnGuards); nearest = returnBox; camera = returnCamera; }
                }
                catch (Win32Exception) { nearest = 0; guards = new List<RuntimeGuard>(binding); }
                catch (InvalidOperationException) { nearest = 0; guards = new List<RuntimeGuard>(binding); }
            }
            if (!memory.Matches(guards)) return;
            long first = header + 4096;
            long slot = first + SelectSlot(first, memory.Number(header), memory.Number(header + 8)) * 4096L;
            var bytes = new byte[4096]; BitConverter.GetBytes(epoch).CopyTo(bytes, 0);
            bool controlling = actor != 0 && desired != null && !followSuspended;
            bool nativeOrbit = controlling && orbit && orbitPlayer != null && groups == 3;
            bool nativeFollow = controlling && followOffset != null && !nativeOrbit && (groups & 1) != 0;
            BitConverter.GetBytes(controlling ? groups | (nativeOrbit ? 4 : nativeFollow ? 8 : 0) : 0).CopyTo(bytes, 8);
            BitConverter.GetBytes(model).CopyTo(bytes, 16); BitConverter.GetBytes(actor).CopyTo(bytes, 24);
            if (desired != null)
            {
                var payload = (float[])desired.Clone();
                if (nativeOrbit) for (int i = 0; i < 3; i++) payload[i] -= orbitPlayer[i] * scale;
                PackPose(bytes, 32, payload);
            }
            BitConverter.GetBytes(scale).CopyTo(bytes, 56); BitConverter.GetBytes(nearest != 0 ? 1 : 0).CopyTo(bytes, 60);
            BitConverter.GetBytes(nearest).CopyTo(bytes, 128); BitConverter.GetBytes(model - 0x80).CopyTo(bytes, 136);
            BitConverter.GetBytes(returnRequest).CopyTo(bytes, CameraFrameCode.ReturnRequest);
            BitConverter.GetBytes(camera).CopyTo(bytes, 232);
            if (nativeFollow)
            {
                for (int i = 0; i < 3; i++) BitConverter.GetBytes(followOffset[i]).CopyTo(bytes, 176 + i * 4);
                BitConverter.GetBytes(followGeneration).CopyTo(bytes, 216);
                long clock = memory.Pointer(guards, memory.Base + 0x1A61888);
                if (memory.TypeName(clock) != "CClock") throw new InvalidOperationException("The camera clock is unavailable.");
                memory.Capture(guards, clock); BitConverter.GetBytes(clock).CopyTo(bytes, 240);
            }
            BitConverter.GetBytes(followDeadzone).CopyTo(bytes, 188); BitConverter.GetBytes(followSpeed).CopyTo(bytes, 192);
            foreach (var constant in new[] { new { Offset = 200, Value = 1f }, new { Offset = 204, Value = 0.5f }, new { Offset = 208, Value = 1f / 6 }, new { Offset = 212, Value = 1f / 24 }, new { Offset = 224, Value = 0.05f } })
                BitConverter.GetBytes(constant.Value).CopyTo(bytes, constant.Offset);
            RuntimeCode.PackGuards(bytes, guards);
            if (!memory.Matches(guards)) return;
            memory.Write(slot, bytes); memory.Write(header, BitConverter.GetBytes(slot));
        }
        private static void PackPose(byte[] bytes, int offset, float[] values)
        {
            for (int i = 0; i < 5; i++) BitConverter.GetBytes(values[i]).CopyTo(bytes, offset + (i < 3 ? i * 4 : i == 3 ? 20 : 16));
        }
        internal void WriteValues(float?[] update, bool relative, Func<float[], float?[]> calculate = null)
        {
            var current = ReadValues(); if (current == null || groups == 0 || actor == 0 || desired == null || followSuspended) return;
            if (calculate != null) update = calculate(current);
            if (update == null || !update.Any(value => value.HasValue)) return;
            for (int i = 0; i < 5; i++)
            {
                if (!update[i].HasValue || (groups & (i < 3 ? 1 : 2)) == 0) continue;
                float previous = current[i]; current[i] = relative ? previous + update[i].Value : update[i].Value;
                if (i == 4 && relative) current[i] = CameraMovement.ClampPitch(current[i], previous);
                if (!RuntimeMemory.Finite(current[i])) throw new ArgumentOutOfRangeException(nameof(update));
            }
            desired = current; Publish(false);
            if (LoadingMemory.Read(memory.Process, GameMemoryProfile.Steam.Camera.PauseMenu, memory.Base))
                memory.WhilePaused(() =>
                {
                    if (binding == null || !memory.Matches(binding) || !LoadingMemory.Read(memory.Process, GameMemoryProfile.Steam.Camera.PauseMenu, memory.Base)) return;
                    var pose = memory.Read(model, 24);
                    for (int i = 0; i < 5; i++) if ((groups & (i < 3 ? 1 : 2)) != 0)
                        BitConverter.GetBytes(current[i]).CopyTo(pose, i < 3 ? i * 4 : i == 3 ? 20 : 16);
                    memory.Write(model, pose);
                });
        }
        internal void Disable()
        {
            groups = 0; model = actor = 0; binding = null; desired = null; orbit = false;
            returnPending = false;
            if (!installed || memory.Process.HasExited) { installed = false; return; }
            memory.WhilePaused(() =>
            {
                memory.Write(header, new byte[8]); memory.Require(hook, jump);
                LevelLockNative.RelocateThreads(memory.Process, rip => rip >= hook + 5 && rip < hook + 8 ? hook + 8 : rip);
                memory.WriteCode(hook, CameraFrameCode.Original); installed = false;
            });
        }
        public void Dispose() { Disable(); memory.Dispose(); }
    }
}
