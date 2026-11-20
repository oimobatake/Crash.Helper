using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Crash.Helper.Memory.Camera;
using Crash.Helper.Memory.Runtime;
using Crash.Helper.Memory.Fade;

namespace Crash.Helper.Memory.Position
{
    // The motion clock and memory writes must not depend on WinForms message traffic.
    internal sealed class PositionService : IDisposable
    {
        private readonly object sync = new object();
        private readonly AutoResetEvent changed = new AutoResetEvent(false);
        private readonly Task worker;
        private Process process;
        private GameMemoryProfile profile;
        private long moduleBase;
        private bool active, acceptInput, stopping, disposed, contiguousPosition;
        private bool fadeWriteDisabled, fadeSuspended, loadingReset;
        private float[] fadePosition;
        private RuntimeMemory engineMemory;
        private PositionCommandPatch enginePatch;
        private PlayerBinding engineBinding;
        private float[] enginePosition;
        private float?[] engineManual = new float?[3];
        private double engineElapsed;
        private long bindingVersion;
        private TaskCompletionSource<bool> teleportCompletion;
        private long teleportCommand;
        private const float FreezeCorrectionTolerance = 0.01f;
        private bool pendingFreezeUpdate, cancelFreezeUpdate;
        private int releaseAxes, submittedReleaseAxes;
        private long releaseCommand;
        internal long BindingVersion { get { lock (sync) return bindingVersion; } }
        internal bool CanWrite { get { lock (sync) return !UsesEngine || (engineBinding != null && engineBinding.Writable); } }
        private bool UsesEngine => ReferenceEquals(profile, GameMemoryProfile.Steam);

        private void ReleaseEngine()
        {
            CompleteTeleport(false);
            ClearFreezeRelease();
            enginePatch?.Dispose(); enginePatch = null;
            engineMemory?.Dispose(); engineMemory = null;
            engineBinding = null; enginePosition = null; engineManual = new float?[3]; engineElapsed = 0;
        }

        internal void ResetForLoading()
        {
            lock (sync)
            {
                CompleteTeleport(false);
                ClearFreezeRelease();
                loadingReset = true; frozen = new bool[3]; adjustments = new int[3]; fadePosition = null; fadeSuspended = false;
                enginePatch?.Cancel(); engineManual = new float?[3]; engineBinding = null; enginePosition = null; engineElapsed = 0;
            }
            changed.Set();
        }

        internal void SetFadeWriteDisabled(bool value)
        {
            lock (sync)
            {
                fadeWriteDisabled = value;
                if (value && !UsesEngine) { fadePosition = null; frozen = new bool[3]; loadingReset = true; }
            }
            changed.Set();
        }
        private bool[] frozen = new bool[3], capture = new bool[3];
        private readonly float[] values = new float[3];
        private int[] directions = new int[3], adjustments = new int[3];
        private float speed;
        private Func<float[]> orientation;

        internal PositionService() => worker = Task.Factory.StartNew(Run, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        internal void Configure(Process target, GameMemoryProfile memoryProfile, bool enabled, bool[] axes,
            bool input, int[] movement, float xyzSpeed, Func<float[]> view, int[] axisAdjustments)
        {
            lock (sync)
            {
                if (stopping) return;
                bool replaced = process == null ? target != null : target == null || process.Id != target.Id || process.HasExited;
                if (replaced)
                {
                    process?.Dispose(); process = target == null ? null : Process.GetProcessById(target.Id);
                    moduleBase = process == null ? 0 : process.MainModule.BaseAddress.ToInt64();
                }
                if (replaced || profile != memoryProfile)
                {
                    ReleaseEngine();
                    capture = (bool[])axes.Clone(); fadePosition = null; fadeSuspended = false; loadingReset = false;
                    contiguousPosition = HasContiguousPosition(memoryProfile?.Position);
                }
                if (!axes.Any(value => value)) loadingReset = false;
                for (int i = 0; i < 3; i++) if (axes[i] && !frozen[i]) capture[i] = true;
                profile = memoryProfile; active = enabled && process != null && profile != null;
                var nextFrozen = loadingReset || (!UsesEngine && fadeWriteDisabled) ? new bool[3] : (bool[])axes.Clone();
                if (UsesEngine && active && engineBinding?.Writable == true)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int bit = 1 << axis;
                        if (nextFrozen[axis]) releaseAxes &= ~bit;
                        else if (frozen[axis])
                        {
                            releaseAxes |= bit;
                            if (pendingFreezeUpdate) cancelFreezeUpdate = true;
                        }
                    }
                }
                frozen = nextFrozen; directions = (int[])movement.Clone();
                acceptInput = input; speed = xyzSpeed; orientation = view;
                adjustments = loadingReset ? new int[3] : (int[])axisAdjustments.Clone();
            }
            changed.Set();
        }

        internal void Write(float?[] update)
        {
            lock (sync)
            {
                CompleteTeleport(false);
                if (UsesEngine)
                {
                    if (!active || stopping || loadingReset) return;
                    for (int i = 0; i < 3; i++) if (update[i].HasValue)
                    {
                        if (!RuntimeMemory.Finite(update[i].Value)) throw new ArgumentOutOfRangeException(nameof(update));
                        engineManual[i] = update[i]; values[i] = update[i].Value; capture[i] = false;
                    }
                    TickEngine(0); changed.Set(); return;
                }
                if (!active || stopping || TransitionBlocked()) return;
                for (int i = 0; i < 3; i++) if (update[i].HasValue)
                {
                    float value = update[i].Value;
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(update));
                    WriteAxis(i, value); values[i] = value; capture[i] = false;
                }
            }
        }
        internal Task<bool> TeleportAsync(float[] target)
        {
            lock (sync)
            {
                if (!UsesEngine) { Write(target.Select(value => (float?)value).ToArray()); return Task.FromResult(true); }
                if (!active || stopping || loadingReset || engineBinding == null || !engineBinding.Writable) return Task.FromResult(false);
                if (target.Length != 3 || !target.All(RuntimeMemory.Finite)) throw new ArgumentOutOfRangeException(nameof(target));
                CompleteTeleport(false);
                var completion = teleportCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                for (int i = 0; i < 3; i++) { engineManual[i] = target[i]; values[i] = target[i]; capture[i] = false; }
                TickEngine(0); changed.Set(); return completion.Task;
            }
        }
        private void CompleteTeleport(bool success)
        {
            var completion = teleportCompletion; teleportCompletion = null; teleportCommand = 0;
            completion?.TrySetResult(success);
        }

        private void ClearFreezeRelease()
        {
            releaseAxes = submittedReleaseAxes = 0;
            releaseCommand = 0;
            pendingFreezeUpdate = cancelFreezeUpdate = false;
        }

        internal float[] ReadValues()
        {
            lock (sync)
            {
                if (UsesEngine)
                {
                    if (!active || stopping || engineMemory == null) return null;
                    var subject = engineMemory.PositionSubject(); if (subject == null) return null;
                    var position = enginePatch.Read(subject);
                    if (position == null && !enginePatch.Pending) enginePatch.Submit(subject, null);
                    return position;
                }
                if (!active || stopping || TransitionBlocked()) return null;
                var paths = profile.Position;
                if (contiguousPosition)
                {
                    // A camera target must use XYZ from one read, not separate game updates.
                    var bytes = Read(Address(paths[0]), 12);
                    return new[] { BitConverter.ToSingle(bytes, 0), BitConverter.ToSingle(bytes, 4), BitConverter.ToSingle(bytes, 8) };
                }
                return Enumerable.Range(0, 3).Select(i => BitConverter.ToSingle(Read(Address(paths[i]), 4), 0)).ToArray();
            }
        }

        private static bool HasContiguousPosition(int[][] paths)
        {
            if (paths == null || paths.Length != 3 || paths[0].Length == 0) return false;
            int last = paths[0].Length - 1;
            for (int axis = 1; axis < 3; axis++)
            {
                if (paths[axis].Length != paths[0].Length || paths[axis][last] != paths[0][last] + axis * 4) return false;
                for (int i = 0; i < last; i++) if (paths[axis][i] != paths[0][i]) return false;
            }
            return true;
        }

        private bool TransitionBlocked()
        {
            float? fade = FadeMemory.Read(process, profile.Fade, moduleBase);
            if (LoadingMemory.Read(process, profile.Loading, moduleBase))
            {
                loadingReset = true; frozen = new bool[3]; adjustments = new int[3]; fadePosition = null; fadeSuspended = false;
                return true;
            }
            if (fadeWriteDisabled) { fadePosition = null; return true; }
            if (!fade.HasValue || fade.Value >= 1.0f)
            {
                if (!fadeSuspended && frozen.Any(value => value))
                    fadePosition = Enumerable.Range(0, 3).Select(i => BitConverter.ToSingle(Read(Address(profile.Position[i]), 4), 0)).ToArray();
                fadeSuspended = true;
                return true;
            }
            if (fadeSuspended)
            {
                fadeSuspended = false;
                if (fadePosition != null && !loadingReset)
                    for (int i = 0; i < 3; i++) { WriteAxis(i, fadePosition[i]); values[i] = fadePosition[i]; capture[i] = false; }
                fadePosition = null;
            }
            return false;
        }

        private bool TickEngine(double seconds)
        {
            if (engineMemory == null)
            {
                engineMemory = new RuntimeMemory(process);
                try { enginePatch = new PositionCommandPatch(engineMemory); }
                catch { engineMemory.Dispose(); engineMemory = null; throw; }
            }
            if (cancelFreezeUpdate)
            {
                enginePatch.CancelQueued();
                cancelFreezeUpdate = false;
            }
            var currentBinding = engineMemory.Player();
            if (currentBinding == null)
            {
                CompleteTeleport(false);
                ClearFreezeRelease();
                if (engineBinding != null) bindingVersion++;
                enginePatch.Cancel(); enginePatch.RestoreWhenIdle(); engineBinding = null; enginePosition = null;
                engineManual = new float?[3]; engineElapsed = 0; frozen = new bool[3]; adjustments = new int[3];
                loadingReset = true; capture = new bool[3]; return false;
            }
            if (!currentBinding.Same(engineBinding))
            {
                enginePatch.Cancel();
                if (engineBinding != null)
                {
                    CompleteTeleport(false);
                    ClearFreezeRelease();
                    engineManual = new float?[3]; frozen = new bool[3]; adjustments = new int[3];
                    directions = new int[3]; loadingReset = true;
                }
                bindingVersion++;
                capture = (bool[])frozen.Clone(); engineElapsed = 0;
            }
            // Pooled actors can keep the same address and ID while their ownership guards change.
            engineBinding = currentBinding;
            enginePosition = enginePatch.Read(engineBinding);
            if (releaseCommand != 0 && enginePatch.CompletedCommand >= releaseCommand)
            {
                if (enginePatch.CompletedCommand == releaseCommand && enginePatch.CommandSucceeded)
                    releaseAxes &= ~submittedReleaseAxes;
                releaseCommand = 0;
                submittedReleaseAxes = 0;
            }
            if (teleportCommand != 0 && enginePatch.CompletedCommand >= teleportCommand)
                CompleteTeleport(enginePatch.CompletedCommand == teleportCommand && enginePatch.CommandSucceeded);
            if (enginePosition == null)
            { if (!enginePatch.Pending) enginePatch.Submit(engineBinding, null); return false; }
            if (!engineBinding.Writable)
            {
                CompleteTeleport(false);
                ClearFreezeRelease();
                enginePatch.Cancel(); enginePatch.RestoreWhenIdle(); engineManual = new float?[3]; frozen = new bool[3]; adjustments = new int[3];
                loadingReset = true; engineElapsed = 0; return false;
            }
            bool moving = frozen.Any(value => value) || adjustments.Any(value => value != 0) || engineManual.Any(value => value.HasValue);
            engineElapsed = moving ? Math.Min(0.05, engineElapsed + Math.Max(0, seconds)) : 0;
            var protectedAxes = releaseAxes == 0 ? frozen :
                frozen.Select((value, axis) => value || (releaseAxes & (1 << axis)) != 0).ToArray();
            enginePatch.ConfigureFreeze(engineBinding, protectedAxes);
            if (enginePatch.Pending) return moving || releaseAxes != 0;
            if (releaseAxes != 0)
            {
                // The physics controller can advance while its entity stores are filtered.
                // Synchronize it to the visible position before those stores are allowed again.
                var releasePosition = new float?[3];
                for (int axis = 0; axis < 3; axis++)
                    if ((releaseAxes & (1 << axis)) != 0) releasePosition[axis] = enginePosition[axis];
                if (enginePatch.Submit(engineBinding, releasePosition))
                {
                    releaseCommand = enginePatch.SubmittedCommand;
                    submittedReleaseAxes = releaseAxes;
                    pendingFreezeUpdate = false;
                }
                engineElapsed = 0;
                return true;
            }
            for (int axis = 0; axis < 3; axis++) if (frozen[axis] && capture[axis])
            { values[axis] = enginePosition[axis]; capture[axis] = false; }
            var update = new float?[3]; var delta = HotkeyDelta(engineElapsed);
            double distance = speed * engineElapsed / 0.03;
            for (int axis = 0; axis < 3; axis++)
            {
                if (engineManual[axis].HasValue) values[axis] = engineManual[axis].Value;
                else if (frozen[axis] || adjustments[axis] != 0)
                    values[axis] = (frozen[axis] ? values[axis] : enginePosition[axis]) + (frozen[axis] ? delta?[axis] ?? 0 : 0) + (float)(adjustments[axis] * distance);
                else continue;
                // The game store filter already holds stationary axes. Repeating the
                // actor teleport at the same position would reset movement during a jump.
                if (RuntimeMemory.Finite(values[axis]) &&
                    (engineManual[axis].HasValue || Math.Abs(values[axis] - enginePosition[axis]) > FreezeCorrectionTolerance))
                    update[axis] = values[axis];
            }
            bool writes = update.Any(value => value.HasValue);
            bool manual = engineManual.Any(value => value.HasValue);
            bool attached = engineMemory.Number(engineBinding.Actor + 0x10) != 0;
            if ((writes || attached) && enginePatch.Submit(engineBinding, writes ? update : null))
            {
                if (manual && teleportCompletion != null && teleportCommand == 0) teleportCommand = enginePatch.SubmittedCommand;
                pendingFreezeUpdate = writes && !manual && frozen.Any(value => value);
                engineManual = new float?[3]; engineElapsed = 0;
            }
            if (!writes && !attached) enginePatch.RestoreWhenIdle();
            if (delta == null && !adjustments.Any(value => value != 0)) engineElapsed = 0;
            // Keep measuring input time even when the first tick does not need a write.
            return moving;
        }

        private long Address(int[] offsets)
        {
            long address = moduleBase;
            for (int i = 0; i < offsets.Length - 1; i++)
            {
                address = BitConverter.ToInt64(Read(address + offsets[i], 8), 0);
                if (address == 0) throw new InvalidOperationException("Player position pointer is unavailable.");
            }
            return address + offsets[offsets.Length - 1];
        }

        private byte[] Read(long address, int length)
        {
            var bytes = new byte[length]; UIntPtr count;
            ProcessNative.Check(ProcessNative.ReadProcessMemory(process.Handle, new IntPtr(address), bytes, (UIntPtr)length, out count) && count.ToUInt64() == (ulong)length, "Read player position");
            return bytes;
        }

        private void WriteAxis(int axis, float value)
        {
            UIntPtr count;
            ProcessNative.Check(ProcessNative.WriteProcessMemory(process.Handle, new IntPtr(Address(profile.Position[axis])), BitConverter.GetBytes(value), (UIntPtr)4, out count) && count.ToUInt64() == 4, "Write player position");
        }

        private float?[] HotkeyDelta(double seconds)
        {
            if (!acceptInput || !directions.Any(value => value != 0) || LoadingMemory.Read(process, profile.Camera?.PauseMenu, moduleBase)) return null;
            if (frozen[0] && frozen[1])
            {
                var view = orientation?.Invoke();
                if (view == null) return null;
                var input = new int[5]; directions.CopyTo(input, 0);
                return CameraMovement.Delta(input, speed, 0, seconds, view[0]);
            }

            // With only one horizontal axis frozen, its own hotkeys move that axis directly.
            var result = new float?[3];
            double length = Math.Sqrt(Enumerable.Range(0, 3).Sum(axis => frozen[axis] ? directions[axis] * directions[axis] : 0));
            if (length > 0)
            {
                double distance = speed * Math.Max(0, Math.Min(seconds, 0.05)) / 0.03 / length;
                for (int axis = 0; axis < 3; axis++)
                    if (frozen[axis] && directions[axis] != 0) result[axis] = (float)(directions[axis] * distance);
            }
            return result;
        }

        private void Run()
        {
            var clock = Stopwatch.StartNew(); double previous = clock.Elapsed.TotalSeconds;
            bool precise = false, wasActive = false;
            try
            {
                while (true)
                {
                    bool ticking;
                    lock (sync)
                    {
                        if (stopping) return;
                        ticking = active;
                        double now = clock.Elapsed.TotalSeconds;
                        double elapsed = wasActive ? now - previous : 0;
                        previous = now;
                        try
                        {
                            if (ticking && UsesEngine) wasActive = TickEngine(elapsed);
                            else
                            {
                              wasActive = ticking && !TransitionBlocked() && (frozen.Any(value => value) || adjustments.Any(value => value != 0));
                              if (wasActive)
                              {
                                for (int i = 0; i < 3; i++) if (frozen[i] && capture[i])
                                {
                                    values[i] = BitConverter.ToSingle(Read(Address(profile.Position[i]), 4), 0); capture[i] = false;
                                }
                                var delta = HotkeyDelta(elapsed);
                                double adjustmentDistance = speed * Math.Max(0, Math.Min(elapsed, 0.05)) / 0.03;
                                for (int i = 0; i < 3; i++)
                                {
                                    if (!frozen[i] && adjustments[i] == 0) continue;
                                    // Buttons edit only their numeric axis, including unfrozen axes and pause menus.
                                    float current = frozen[i] ? values[i] : BitConverter.ToSingle(Read(Address(profile.Position[i]), 4), 0);
                                    float value = current + (frozen[i] ? delta?[i] ?? 0 : 0) + (float)(adjustments[i] * adjustmentDistance);
                                    if (float.IsNaN(value) || float.IsInfinity(value)) continue;
                                    values[i] = value;
                                    WriteAxis(i, value);
                                }
                              }
                            }
                        }
                        catch (Exception ex) { CompleteTeleport(false); wasActive = false; HelperLog.Error("Update player position", ex); }
                    }
                    if (ticking && !precise) precise = timeBeginPeriod(1) == 0;
                    else if (!ticking && precise) { timeEndPeriod(1); precise = false; }
                    changed.WaitOne(ticking ? 8 : Timeout.Infinite);
                }
            }
            finally { lock (sync) ReleaseEngine(); if (precise) timeEndPeriod(1); }
        }

        internal Task ShutdownAsync()
        {
            lock (sync) { stopping = true; CompleteTeleport(false); }
            changed.Set(); return worker;
        }

        public void Dispose()
        {
            if (disposed) return;
            ShutdownAsync().GetAwaiter().GetResult(); process?.Dispose(); changed.Dispose(); disposed = true;
        }

        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);
    }
}
