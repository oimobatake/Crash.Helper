using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Crash.Helper.Memory.Camera
{
    internal sealed class CameraService : IDisposable
    {
        private readonly object sync = new object();
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource pending;
        private CameraPatch patch;
        private int generation;
        private bool active;
        private readonly AutoResetEvent motionChanged = new AutoResetEvent(false);
        private readonly Task motionWorker;
        private int[] directions = new int[5];
        private float xyzSpeed = 0.5f, rotationSpeed = 0.01f;
        private bool stopping, disposed, shutdownComplete;

        internal CameraService()
        {
            motionWorker = Task.Factory.StartNew(MoveLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        internal void SetMovement(int[] value, float xyz, float rotation)
        {
            lock (sync)
            {
                if (stopping || disposed) return;
                if (directions.SequenceEqual(value) && xyzSpeed == xyz && rotationSpeed == rotation) return;
                directions = (int[])value.Clone();
                xyzSpeed = xyz;
                rotationSpeed = rotation;
                motionChanged.Set();
            }
        }

        private void MoveLoop()
        {
            bool preciseTimer = false;
            var clock = Stopwatch.StartNew();
            double previous = clock.Elapsed.TotalSeconds;
            try
            {
                while (true)
                {
                    int[] input;
                    float xyz, rotation;
                    lock (sync)
                    {
                        if (stopping) return;
                        input = (int[])directions.Clone();
                        xyz = xyzSpeed;
                        rotation = rotationSpeed;
                    }
                    bool moving = input.Any(value => value != 0);
                    if (!moving)
                    {
                        if (preciseTimer) { timeEndPeriod(1); preciseTimer = false; }
                        motionChanged.WaitOne();
                        previous = clock.Elapsed.TotalSeconds;
                        continue;
                    }
                    if (!preciseTimer) preciseTimer = timeBeginPeriod(1) == 0;
                    double now = clock.Elapsed.TotalSeconds;
                    double elapsed = now - previous;
                    previous = now;
                    if (gate.Wait(0))
                    {
                        try
                        {
                            bool apply;
                            lock (sync) apply = !stopping && active && directions.SequenceEqual(input);
                            if (apply) patch.Move(input, xyz, rotation, elapsed);
                        }
                        catch (Exception ex)
                        {
                            HelperLog.Error("Move camera", ex);
                            lock (sync) directions = new int[5];
                        }
                        finally { gate.Release(); }
                    }
                    motionChanged.WaitOne(8);
                }
            }
            finally { if (preciseTimer) timeEndPeriod(1); }
        }

        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);

        internal Task ConfigureAsync(Process process, CameraMemoryProfile profile, bool xyz, bool yawPitch)
        {
            CancellationTokenSource request;
            lock (sync)
            {
                generation++;
                pending?.Cancel();
                pending = request = new CancellationTokenSource();
            }
            return Task.Run(async () =>
            {
                bool entered = false;
                try
                {
                    await gate.WaitAsync(request.Token).ConfigureAwait(false);
                    entered = true;
                    request.Token.ThrowIfCancellationRequested();
                    active = false;
                    if (process == null || profile == null) { patch?.Disable(); return; }
                    if (patch != null && (patch.HasExited || patch.ProcessId != process.Id)) { patch.Dispose(); patch = null; }
                    if (patch == null) patch = new CameraPatch(process, profile);
                    try
                    {
                        // The executable may not expose its camera code immediately after startup.
                        while (true)
                        {
                            request.Token.ThrowIfCancellationRequested();
                            try { patch.Enable(xyz, yawPitch, request.Token); break; }
                            catch (CameraSignatureNotFoundException ex)
                            {
                                HelperLog.Error("Waiting for camera code", ex);
                                await Task.Delay(1000, request.Token).ConfigureAwait(false);
                                if (patch.HasExited) return;
                            }
                        }
                        active = true;
                    }
                    catch { patch.Disable(); throw; }
                }
                finally
                {
                    if (entered) gate.Release();
                    lock (sync)
                    {
                        if (pending == request) pending = null;
                        request.Dispose();
                    }
                }
            });
        }

        internal Task<float[]> ReadAsync()
        {
            int version;
            lock (sync) version = generation;
            return Task.Run(async () =>
            {
                await gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    lock (sync) if (version != generation) return null;
                    return active ? patch.ReadValues() : null;
                }
                finally { gate.Release(); }
            });
        }

        internal Task WriteAsync(float?[] values, bool relative = false)
        {
            var copy = (float?[])values.Clone();
            int version;
            lock (sync) version = generation;
            return Task.Run(async () =>
            {
                await gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    lock (sync) if (version != generation) return;
                    if (active) patch.WriteValues(copy, relative);
                }
                finally { gate.Release(); }
            });
        }

        internal async Task ShutdownAsync()
        {
            lock (sync)
            {
                if (disposed || shutdownComplete) return;
                stopping = true;
                directions = new int[5];
                motionChanged.Set();
            }
            await motionWorker.ConfigureAwait(false);
            await ConfigureAsync(null, null, false, false).ConfigureAwait(false);
            await gate.WaitAsync().ConfigureAwait(false);
            try { patch?.Dispose(); patch = null; shutdownComplete = true; }
            finally { gate.Release(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            ShutdownAsync().GetAwaiter().GetResult();
            lock (sync) { disposed = true; motionChanged.Dispose(); }
        }
    }
}
