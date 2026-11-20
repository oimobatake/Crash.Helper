using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Crash.Helper.Memory.Camera
{
    internal sealed class CameraPatch : IDisposable
    {
        private readonly CameraProcessMemory memory;
        private readonly CameraMemoryProfile profile;
        private readonly CameraFramePatch owned;
        private readonly long positionCode, rotationCode;
        private readonly long moduleBase;
        private long injection, cave, pointerStorage;
        private byte[] capturePatch;
        private bool installed, freezePosition, freezeRotation;

        internal CameraPatch(Process process, CameraMemoryProfile profile)
            : this(process, process.MainModule.BaseAddress.ToInt64(), process.MainModule.ModuleMemorySize, profile) { }

        internal CameraPatch(Process process, long moduleBase, int moduleSize, CameraMemoryProfile profile)
        {
            this.moduleBase = moduleBase;
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            memory = new CameraProcessMemory(process, moduleBase, moduleSize, profile.CapturePattern);
            if (ReferenceEquals(profile, CameraMemoryProfile.Steam) && moduleSize == GameMemoryProfile.Steam.ModuleSize)
                owned = new CameraFramePatch(new Runtime.RuntimeMemory(process));
            positionCode = moduleBase + profile.PositionCodeOffset;
            rotationCode = moduleBase + profile.RotationCodeOffset;
        }

        internal int ProcessId => memory.Process.Id;
        internal bool HasExited => memory.Process.HasExited;

        internal void Enable(bool xyz, bool yawPitch, CancellationToken cancellation)
        {
            if (owned != null)
            {
                cancellation.ThrowIfCancellationRequested(); owned.Enable(xyz, yawPitch);
                installed = true; freezePosition = xyz; freezeRotation = yawPitch; return;
            }
            if (cave == 0)
            {
                injection = memory.FindInjection(cancellation);
                var current = memory.Read(injection, 7);
                if (current[0] == 0xE9)
                {
                    memory.WhilePaused(() =>
                    {
                        RequireBytes(injection, current);
                        if (!memory.IsRecoverableCapture(injection)) throw new InvalidOperationException("The camera hook changed during recovery.");
                        cave = injection + 5 + BitConverter.ToInt32(current, 1);
                        pointerStorage = cave + 4096;
                        capturePatch = current;
                        installed = true;
                        freezePosition = memory.Read(positionCode, 3).All(value => value == 0x90);
                        freezeRotation = memory.Read(rotationCode, 5).All(value => value == 0x90);
                        WriteOwner();
                    });
                }
                else
                {
                    long allocated = memory.AllocateNear(injection, cancellation);
                    pointerStorage = allocated + 4096;
                    memory.Write(allocated, CameraCaptureCode.Build(allocated, pointerStorage, injection + 7));
                    memory.ProtectCode(allocated);
                    capturePatch = CameraCaptureCode.Jump(injection, allocated, 7);
                    cave = allocated;
                }
            }
            cancellation.ThrowIfCancellationRequested();
            memory.WhilePaused(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                if (!installed)
                {
                    RequireBytes(injection, CameraCaptureCode.CaptureOriginal);
                    RequireBytes(positionCode, CameraCaptureCode.PositionOriginal);
                    RequireBytes(rotationCode, CameraCaptureCode.RotationOriginal);
                    RelocateCapture(true);
                    memory.Write(pointerStorage, new byte[8]);
                    WriteOwner();
                    installed = true;
                    memory.WriteCode(injection, capturePatch);
                }
                SetFreeze(positionCode, CameraCaptureCode.PositionOriginal, xyz, ref freezePosition);
                SetFreeze(rotationCode, CameraCaptureCode.RotationOriginal, yawPitch, ref freezeRotation);
            });
        }

        private void RequireBytes(long address, byte[] expected)
        {
            var actual = memory.Read(address, expected.Length);
            if (!actual.SequenceEqual(expected))
                throw new InvalidOperationException($"Camera instructions do not match at module+0x{address - moduleBase:X}. Expected {BitConverter.ToString(expected)}, found {BitConverter.ToString(actual)}. Check the game version and disable other camera tools.");
        }

        private void RelocateCapture(bool installing)
        {
            ProcessNative.RelocateThreads(memory.Process, rip =>
            {
                if (installing && rip == injection + 3) return cave + 3;
                if (!installing && (rip == injection + 5 || rip == injection + 6)) return injection + 7;
                if (rip > injection && rip < injection + 7)
                    throw new InvalidOperationException("A game thread is inside an unexpected camera instruction.");
                return rip;
            });
        }

        private void SetFreeze(long address, byte[] original, bool enabled, ref bool applied)
        {
            if (enabled == applied) return;
            var nops = Enumerable.Repeat((byte)0x90, original.Length).ToArray();
            var current = memory.Read(address, original.Length);
            if (!enabled && current.SequenceEqual(original)) { applied = false; return; }
            if (!current.SequenceEqual(enabled ? original : nops))
                throw new InvalidOperationException("Camera instructions were changed by another tool. No code was overwritten.");
            ProcessNative.RelocateThreads(memory.Process, rip =>
            {
                if (rip <= address || rip >= address + original.Length) return rip;
                if (!enabled) return address + original.Length;
                throw new InvalidOperationException("A game thread is inside an unexpected camera instruction.");
            });
            if (enabled) applied = true;
            memory.WriteCode(address, enabled ? nops : original);
            applied = enabled;
        }

        internal float[] ReadValues()
        {
            long address;
            byte[] snapshot;
            return ReadSnapshot(out address, out snapshot);
        }

        private float[] ReadSnapshot(out long address, out byte[] snapshot)
        {
            address = 0;
            snapshot = null;
            if (!installed || HasExited) return null;
            if (owned != null) return owned.ReadValues();
            try
            {
                address = BitConverter.ToInt64(memory.Read(pointerStorage, 8), 0);
                if (address <= 0) return null;
                int length = profile.ValueOffsets.Max() + sizeof(float);
                snapshot = memory.Read(address, length);
                if (BitConverter.ToInt64(memory.Read(pointerStorage, 8), 0) != address) return null;
                var values = new float[5];
                for (int i = 0; i < values.Length; i++) values[i] = BitConverter.ToSingle(snapshot, profile.ValueOffsets[i]);
                return values.All(IsFinite) ? values : null;
            }
            catch (Win32Exception) { return null; } // The camera can be replaced during a level transition.
        }

        internal void WriteValues(float?[] values, bool relative = false, bool pauseGame = true)
        {
            if (!installed || HasExited) return;
            if (pauseGame) memory.WhilePaused(() => WriteValuesCore(values, relative));
            else WriteValuesCore(values, relative);
        }

        private void WriteOwner()
        {
            var owner = new byte[48];
            CameraCaptureCode.OwnerMarker.CopyTo(owner, 0);
            using (var helper = Process.GetCurrentProcess())
            {
                BitConverter.GetBytes(helper.Id).CopyTo(owner, 24);
                BitConverter.GetBytes(helper.StartTime.ToUniversalTime().Ticks).CopyTo(owner, 32);
            }
            memory.Write(pointerStorage + 16, owner);
        }

        internal void Move(int[] directions, float xyzSpeed, float rotationSpeed, double seconds,
            bool followPitch = false, double mouseYaw = 0, double mousePitch = 0, CameraFollow follow = null, float[] player = null)
        {
            if (!installed || HasExited) return;
            if (IsPauseMenuOpen()) return;
            owned?.SetFollowAvailable(follow == null || player != null);
            if (follow != null && player == null) return;
            bool orbiting = follow != null && follow.Orbiting && freezePosition;
            owned?.SetOrbit(orbiting, player, follow?.Scale ?? 1);
            owned?.SetNativeFollow(null);
            // Freeze removes the game writers. Movement must not suspend the entire game each frame.
            WriteValuesCore(null, true, current =>
            {
                if (orbiting)
                {
                    var center = owned?.OrbitCenter(player) ?? player;
                    return follow.Orbit(current, directions, xyzSpeed, rotationSpeed, seconds, mouseYaw, mousePitch, center);
                }
                var delta = CameraMovement.Delta(directions, xyzSpeed, rotationSpeed, seconds,
                    current[3], current[4], followPitch, mouseYaw, mousePitch);
                if (follow == null || !freezePosition) return delta;
                var update = follow.Apply(current, delta, player, seconds);
                owned?.SetNativeFollow(follow);
                return update;
            });
        }

        private bool IsPauseMenuOpen()
        {
            if (profile.PauseMenu == null || profile.PauseMenu.Length == 0) return false;
            try
            {
                long address = moduleBase;
                for (int i = 0; i < profile.PauseMenu.Length - 1; i++)
                {
                    address = BitConverter.ToInt64(memory.Read(address + profile.PauseMenu[i], 8), 0);
                    if (address == 0) return true;
                }
                return memory.Read(address + profile.PauseMenu[profile.PauseMenu.Length - 1], 1)[0] != 0;
            }
            catch (Win32Exception ex)
            {
                HelperLog.Error("Read pause menu for camera input", ex);
                return true;
            }
        }

        private void WriteValuesCore(float?[] values, bool relative, Func<float[], float?[]> calculate = null)
        {
            if (owned != null) { owned.WriteValues(values, relative, calculate); return; }
            if (profile.Loading != null && LoadingMemory.Read(memory.Process, profile.Loading, moduleBase)) return;
            long address;
            byte[] snapshot;
            var current = ReadSnapshot(out address, out snapshot);
            if (current == null || BitConverter.ToInt64(memory.Read(pointerStorage, 8), 0) != address) return;
            if (calculate != null) values = calculate(current);
            for (int i = 0; i < 5; i++)
            {
                if (!values[i].HasValue || !(i < 3 ? freezePosition : freezeRotation)) continue;
                float previous = current[i];
                current[i] = relative ? current[i] + values[i].Value : values[i].Value;
                if (i == 4 && relative) current[i] = CameraMovement.ClampPitch(current[i], previous);
                if (!IsFinite(current[i])) throw new ArgumentOutOfRangeException(nameof(values), "Camera values must be finite.");
            }
            bool writePosition = freezePosition && (values[0].HasValue || values[1].HasValue || values[2].HasValue);
            bool writeRotation = freezeRotation && (values[3].HasValue || values[4].HasValue);
            if (writePosition && writeRotation)
            {
                // Submit one pose rather than exposing new XYZ with the old yaw/pitch.
                // Preserve the unowned field at +0x0C from the same snapshot.
                for (int i = 0; i < current.Length; i++) BitConverter.GetBytes(current[i]).CopyTo(snapshot, profile.ValueOffsets[i]);
                memory.Write(address, snapshot);
                return;
            }
            if (writePosition)
            {
                var bytes = new byte[12];
                for (int i = 0; i < 3; i++) BitConverter.GetBytes(current[i]).CopyTo(bytes, profile.ValueOffsets[i]);
                memory.Write(address, bytes);
            }
            if (writeRotation)
            {
                var bytes = new byte[8];
                for (int i = 3; i < 5; i++) BitConverter.GetBytes(current[i]).CopyTo(bytes, profile.ValueOffsets[i] - 0x10);
                memory.Write(address + 0x10, bytes);
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal void Disable()
        {
            if (owned != null) { owned.Disable(); installed = freezePosition = freezeRotation = false; return; }
            if (HasExited) { installed = freezePosition = freezeRotation = false; return; }
            if (!installed && !freezePosition && !freezeRotation) return;
            memory.WhilePaused(() =>
            {
                // Restore independent patches even if another tool changed one of them.
                var failures = new List<Exception>();
                try { SetFreeze(positionCode, CameraCaptureCode.PositionOriginal, false, ref freezePosition); }
                catch (Exception ex) { failures.Add(ex); }
                try { SetFreeze(rotationCode, CameraCaptureCode.RotationOriginal, false, ref freezeRotation); }
                catch (Exception ex) { failures.Add(ex); }
                try
                {
                    if (installed)
                    {
                        if (!memory.Read(injection, 7).SequenceEqual(CameraCaptureCode.CaptureOriginal))
                        {
                            RequireBytes(injection, capturePatch);
                            RelocateCapture(false);
                            memory.WriteCode(injection, CameraCaptureCode.CaptureOriginal);
                        }
                        installed = false;
                    }
                    RequireBytes(injection, CameraCaptureCode.CaptureOriginal);
                }
                catch (Exception ex) { failures.Add(ex); }
                if (failures.Count > 0) throw new AggregateException("Could not restore camera instructions.", failures);
            });
        }
        internal void ReturnToPlayer()
        {
            if (owned != null) { owned.ReturnToPlayer(); installed = true; freezePosition = freezeRotation = false; }
            else Disable();
        }

        public void Dispose()
        {
            Disable();
            owned?.Dispose();
            memory.Dispose();
            // Reuse published pages on normal off/on cycles. Keep them until process exit
            // when disposing, because an in-flight game thread can still be in the cave.
        }
    }
}
