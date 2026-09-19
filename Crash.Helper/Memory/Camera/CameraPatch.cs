using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Memory.Camera
{
    internal sealed class CameraPatch : IDisposable
    {
        private readonly CameraProcessMemory memory;
        private readonly CameraMemoryProfile profile;
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
            positionCode = moduleBase + profile.PositionCodeOffset;
            rotationCode = moduleBase + profile.RotationCodeOffset;
        }

        internal int ProcessId => memory.Process.Id;
        internal bool HasExited => memory.Process.HasExited;

        internal void Enable(bool xyz, bool yawPitch, CancellationToken cancellation)
        {
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
                    memory.Write(allocated, CameraCode.Build(allocated, pointerStorage, injection + 7));
                    memory.ProtectCode(allocated);
                    capturePatch = CameraCode.Jump(injection, allocated, 7);
                    cave = allocated;
                }
            }
            cancellation.ThrowIfCancellationRequested();
            memory.WhilePaused(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                if (!installed)
                {
                    RequireBytes(injection, CameraCode.CaptureOriginal);
                    RequireBytes(positionCode, CameraCode.PositionOriginal);
                    RequireBytes(rotationCode, CameraCode.RotationOriginal);
                    RelocateCapture(true);
                    memory.Write(pointerStorage, new byte[8]);
                    WriteOwner();
                    installed = true;
                    memory.WriteCode(injection, capturePatch);
                }
                SetFreeze(positionCode, CameraCode.PositionOriginal, xyz, ref freezePosition);
                SetFreeze(rotationCode, CameraCode.RotationOriginal, yawPitch, ref freezeRotation);
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
            LevelLockNative.RelocateThreads(memory.Process, rip =>
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
            LevelLockNative.RelocateThreads(memory.Process, rip =>
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
            if (!installed || HasExited) return null;
            try
            {
                long address = BitConverter.ToInt64(memory.Read(pointerStorage, 8), 0);
                if (address <= 0) return null;
                int length = profile.ValueOffsets.Max() + sizeof(float);
                var bytes = memory.Read(address, length);
                if (BitConverter.ToInt64(memory.Read(pointerStorage, 8), 0) != address) return null;
                var values = profile.ValueOffsets.Select(offset => BitConverter.ToSingle(bytes, offset)).ToArray();
                return values.All(IsFinite) ? values : null;
            }
            catch (Win32Exception) { return null; } // The camera can be replaced during a level transition.
        }

        internal void WriteValues(float?[] values, bool relative = false)
        {
            if (!installed || HasExited) return;
            memory.WhilePaused(() => WriteValuesCore(values, relative));
        }

        private void WriteOwner()
        {
            var owner = new byte[48];
            CameraCode.OwnerMarker.CopyTo(owner, 0);
            using (var helper = Process.GetCurrentProcess())
            {
                BitConverter.GetBytes(helper.Id).CopyTo(owner, 24);
                BitConverter.GetBytes(helper.StartTime.ToUniversalTime().Ticks).CopyTo(owner, 32);
            }
            memory.Write(pointerStorage + 16, owner);
        }

        internal void Move(int[] directions, float xyzSpeed, float rotationSpeed, double seconds,
            bool followPitch = false, double mouseYaw = 0, double mousePitch = 0)
        {
            if (!installed || HasExited) return;
            if (IsPauseMenuOpen()) return;
            // Freeze removes the game writers. Movement must not suspend the entire game each frame.
            WriteValuesCore(null, true, current => CameraMovement.Delta(directions, xyzSpeed, rotationSpeed, seconds,
                current[3], current[4], followPitch, mouseYaw, mousePitch));
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
            if (profile.Loading != null && LoadingMemory.Read(memory.Process, profile.Loading, moduleBase)) return;
            long address = BitConverter.ToInt64(memory.Read(pointerStorage, 8), 0);
            var current = ReadValues();
            if (current == null || BitConverter.ToInt64(memory.Read(pointerStorage, 8), 0) != address) return;
            if (calculate != null) values = calculate(current);
            for (int i = 0; i < 5; i++)
            {
                if (!values[i].HasValue || !(i < 3 ? freezePosition : freezeRotation)) continue;
                current[i] = relative ? current[i] + values[i].Value : values[i].Value;
                if (!IsFinite(current[i])) throw new ArgumentOutOfRangeException(nameof(values), "Camera values must be finite.");
            }
            // Write each frozen group together instead of issuing independent per-axis updates.
            if (freezePosition && values.Take(3).Any(value => value.HasValue))
            {
                var bytes = new byte[12];
                for (int i = 0; i < 3; i++) BitConverter.GetBytes(current[i]).CopyTo(bytes, profile.ValueOffsets[i]);
                memory.Write(address, bytes);
            }
            if (freezeRotation && values.Skip(3).Any(value => value.HasValue))
            {
                var bytes = new byte[8];
                for (int i = 3; i < 5; i++) BitConverter.GetBytes(current[i]).CopyTo(bytes, profile.ValueOffsets[i] - 0x10);
                memory.Write(address + 0x10, bytes);
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal void Disable()
        {
            if (HasExited) { installed = freezePosition = freezeRotation = false; return; }
            if (!installed && !freezePosition && !freezeRotation) return;
            memory.WhilePaused(() =>
            {
                // Restore independent patches even if another tool changed one of them.
                var failures = new List<Exception>();
                try { SetFreeze(positionCode, CameraCode.PositionOriginal, false, ref freezePosition); }
                catch (Exception ex) { failures.Add(ex); }
                try { SetFreeze(rotationCode, CameraCode.RotationOriginal, false, ref freezeRotation); }
                catch (Exception ex) { failures.Add(ex); }
                try
                {
                    if (installed)
                    {
                        if (!memory.Read(injection, 7).SequenceEqual(CameraCode.CaptureOriginal))
                        {
                            RequireBytes(injection, capturePatch);
                            RelocateCapture(false);
                            memory.WriteCode(injection, CameraCode.CaptureOriginal);
                        }
                        installed = false;
                    }
                    RequireBytes(injection, CameraCode.CaptureOriginal);
                }
                catch (Exception ex) { failures.Add(ex); }
                if (failures.Count > 0) throw new AggregateException("Could not restore camera instructions.", failures);
            });
        }

        public void Dispose()
        {
            Disable();
            memory.Dispose();
            // Reuse published pages on normal off/on cycles. Keep them until process exit
            // when disposing, because an in-flight game thread can still be in the cave.
        }
    }
}
