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
        private bool active, acceptInput, stopping, disposed;
        private bool fadeWriteDisabled, fadeSuspended, loadingReset;
        private float[] fadePosition;

        internal void ResetForLoading()
        {
            lock (sync) { loadingReset = true; frozen = new bool[3]; fadePosition = null; fadeSuspended = false; }
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
        private int[] directions = new int[3];
        private float speed;
        private Func<float[]> orientation;

        internal PositionMotionService() => worker = Task.Factory.StartNew(Run, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        internal void Configure(Process target, GameMemoryProfile memoryProfile, bool enabled, bool[] axes,
            bool input, int[] movement, float xyzSpeed, Func<float[]> view)
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
                if (replaced || profile != memoryProfile) { capture = (bool[])axes.Clone(); fadePosition = null; fadeSuspended = false; loadingReset = false; }
                if (!axes.Any(value => value)) loadingReset = false;
                for (int i = 0; i < 3; i++) if (axes[i] && !frozen[i]) capture[i] = true;
                profile = memoryProfile; active = enabled && process != null && profile != null;
                frozen = loadingReset || fadeWriteDisabled ? new bool[3] : (bool[])axes.Clone(); directions = (int[])movement.Clone();
                acceptInput = input; speed = xyzSpeed; orientation = view;
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
                return Enumerable.Range(0, 3).Select(i => BitConverter.ToSingle(Read(Address(profile.Position[i]), 4), 0)).ToArray();
            }
        }

        private bool TransitionBlocked()
        {
            float? fade = FadeMemory.Read(process, profile.Fade, moduleBase);
            if (LoadingMemory.Read(process, profile.Loading, moduleBase))
            {
                loadingReset = true; frozen = new bool[3]; fadePosition = null; fadeSuspended = false;
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
                            wasActive = ticking && !TransitionBlocked() && frozen.Any(value => value);
                            if (wasActive)
                            {
                                for (int i = 0; i < 3; i++) if (frozen[i] && capture[i])
                                {
                                    values[i] = BitConverter.ToSingle(Read(Address(profile.Position[i]), 4), 0); capture[i] = false;
                                }
                                var view = acceptInput && !LoadingMemory.Read(process, profile.Camera?.PauseMenu, moduleBase) && directions.Any(value => value != 0) ? orientation?.Invoke() : null;
                                if (view != null)
                                {
                                    var input = new int[5]; directions.CopyTo(input, 0);
                                    var delta = CameraMovement.Delta(input, speed, 0, elapsed, view[0]);
                                    for (int i = 0; i < 3; i++) if (frozen[i] && delta[i].HasValue)
                                    {
                                        float value = values[i] + delta[i].Value;
                                        if (!float.IsNaN(value) && !float.IsInfinity(value)) values[i] = value;
                                    }
                                }
                                for (int i = 0; i < 3; i++) if (frozen[i]) WriteAxis(i, values[i]);
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
