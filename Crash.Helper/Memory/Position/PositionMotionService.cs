using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Crash.Helper.Memory.Camera;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Memory.Position
{
    // The motion clock and memory writes must not depend on WinForms message traffic.
    internal sealed class PositionMotionService : IDisposable
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

        internal void ResetForLoading()
        {
            lock (sync) { loadingReset = true; frozen = new bool[3]; adjustments = new int[3]; fadePosition = null; fadeSuspended = false; }
            changed.Set();
        }

        internal void SetFadeWriteDisabled(bool value)
        {
            lock (sync)
            {
                fadeWriteDisabled = value;
                if (value) { fadePosition = null; frozen = new bool[3]; loadingReset = true; }
            }
            changed.Set();
        }
        private bool[] frozen = new bool[3], capture = new bool[3];
        private readonly float[] values = new float[3];
        private int[] directions = new int[3], adjustments = new int[3];
        private float speed;
        private Func<float[]> orientation;

        internal PositionMotionService() => worker = Task.Factory.StartNew(Run, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

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
                    capture = (bool[])axes.Clone(); fadePosition = null; fadeSuspended = false; loadingReset = false;
                    contiguousPosition = HasContiguousPosition(memoryProfile?.Position);
                }
                if (!axes.Any(value => value)) loadingReset = false;
                for (int i = 0; i < 3; i++) if (axes[i] && !frozen[i]) capture[i] = true;
                profile = memoryProfile; active = enabled && process != null && profile != null;
                frozen = loadingReset || fadeWriteDisabled ? new bool[3] : (bool[])axes.Clone(); directions = (int[])movement.Clone();
                acceptInput = input; speed = xyzSpeed; orientation = view;
                adjustments = (int[])axisAdjustments.Clone();
            }
            changed.Set();
        }

        internal void Write(float?[] update)
        {
            lock (sync)
            {
                if (!active || stopping || TransitionBlocked()) return;
                for (int i = 0; i < 3; i++) if (update[i].HasValue)
                {
                    float value = update[i].Value;
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(update));
                    WriteAxis(i, value); values[i] = value; capture[i] = false;
                }
            }
        }

        internal float[] ReadValues()
        {
            lock (sync)
            {
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
            LevelLockNative.Check(LevelLockNative.ReadProcessMemory(process.Handle, new IntPtr(address), bytes, (UIntPtr)length, out count) && count.ToUInt64() == (ulong)length, "Read player position");
            return bytes;
        }

        private void WriteAxis(int axis, float value)
        {
            UIntPtr count;
            LevelLockNative.Check(LevelLockNative.WriteProcessMemory(process.Handle, new IntPtr(Address(profile.Position[axis])), BitConverter.GetBytes(value), (UIntPtr)4, out count) && count.ToUInt64() == 4, "Write player position");
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
                        catch (Exception ex) { wasActive = false; HelperLog.Error("Update player position", ex); }
                    }
                    if (ticking && !precise) precise = timeBeginPeriod(1) == 0;
                    else if (!ticking && precise) { timeEndPeriod(1); precise = false; }
                    changed.WaitOne(ticking ? 8 : Timeout.Infinite);
                }
            }
            finally { if (precise) timeEndPeriod(1); }
        }

        internal Task ShutdownAsync()
        {
            lock (sync) stopping = true;
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
