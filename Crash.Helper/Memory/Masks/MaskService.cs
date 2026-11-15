using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Crash.Helper.Memory.Masks
{
    // This worker never waits for WinForms. Input wakes it immediately; readback runs every 8 ms.
    internal sealed class MaskService : IDisposable
    {
        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Queue<Tuple<int, MaskBinding>> inputs = new Queue<Tuple<int, MaskBinding>>();
        private readonly MaskFreezePolicy policy = new MaskFreezePolicy();
        private readonly Task worker;
        private int processId, storedMasks, revision;
        private bool freeze, formOnHit, disposed, manualTarget;
        private MaskSnapshot latest = MaskSnapshot.Unavailable;
        private int? pausedTarget;
        private MaskBinding pausedBinding;
        private TaskCompletionSource<bool> stopping;
        internal MaskSnapshot Latest { get { lock (gate) return latest; } }
        internal int DisplayCount
        {
            get
            {
                lock (gate)
                {
                    if (pausedTarget.HasValue && pausedBinding.SameScene(latest.Binding)) return pausedTarget.Value;
                    return latest.Paused && manualTarget && freeze && !formOnHit ? storedMasks : latest.Count;
                }
            }
        }
        private void ClearPausedTarget() { pausedTarget = null; pausedBinding = null; }

        internal MaskService() { worker = Task.Run((Action)Run); }
        internal void Configure(Process process, bool frozen, int stored, bool form)
        {
            lock (gate)
            {
                if (disposed) return;
                int id = process?.Id ?? 0;
                if (processId == id && freeze == frozen && storedMasks == stored && formOnHit == form) return;
                if (id != processId || form != formOnHit || !latest.CanConfigure) ClearPausedTarget();
                processId = id; freeze = frozen; storedMasks = Math.Max(0, Math.Min(3, stored)); formOnHit = form;
                manualTarget = false;
                revision++; inputs.Clear();
            }
            wake.Set();
        }
        internal void SetManualTarget(int target)
        {
            lock (gate)
            {
                target = Math.Max(0, Math.Min(3, target));
                if (disposed || !freeze || formOnHit || storedMasks == target) return;
                storedMasks = target; manualTarget = true; revision++; inputs.Clear();
                ClearPausedTarget();
            }
            wake.Set();
        }
        internal void Change(int amount)
        {
            lock (gate)
            {
                if (disposed || !latest.CanConfigure) return;
                if (latest.Paused || pausedTarget.HasValue)
                {
                    int current = pausedTarget.HasValue && pausedBinding.SameScene(latest.Binding) ? pausedTarget.Value : latest.Count;
                    pausedTarget = (int)Math.Max(0L, Math.Min(3L, (long)current + amount));
                    pausedBinding = latest.Binding;
                    // Paused edits select a final count, so + then - does not briefly trigger a third pickup.
                    revision++; inputs.Clear();
                }
                else
                {
                    if (!latest.Ready || (latest.Invincible && amount > 0)) return;
                    int count = (int)Math.Min(16L, Math.Abs((long)amount));
                    while (count-- > 0 && inputs.Count < 32) inputs.Enqueue(Tuple.Create(Math.Sign(amount), latest.Binding));
                }
            }
            wake.Set();
        }
        internal Task StopAsync()
        {
            lock (gate)
            {
                if (disposed) return worker;
                processId = 0; revision++; inputs.Clear(); latest = MaskSnapshot.Unavailable;
                ClearPausedTarget();
                if (stopping == null) stopping = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                wake.Set(); return stopping.Task;
            }
        }
        private void Publish(MaskSnapshot value)
        {
            lock (gate)
            {
                latest = value;
                if (pausedTarget.HasValue && (!value.CanConfigure || !pausedBinding.SameScene(value.Binding))) ClearPausedTarget();
            }
        }
        private void CompleteStop(Exception error = null)
        {
            lock (gate)
            {
                if (stopping == null) return;
                if (error == null) stopping.TrySetResult(true); else stopping.TrySetException(error);
                stopping = null;
            }
        }
        private static void Close(ref MaskCommandPatch patch, ref MaskMemory memory)
        {
            if (patch != null && !memory.Process.HasExited)
            {
                patch.Cancel(); var watch = Stopwatch.StartNew();
                while (patch.Pending && watch.ElapsedMilliseconds < 1000) { patch.Poll(); Thread.Sleep(1); }
            }
            patch?.Dispose(); patch = null; memory?.Dispose(); memory = null;
        }
        private void Run()
        {
            MaskMemory memory = null; MaskCommandPatch patch = null; MaskBinding previousScene = null;
            Tuple<int, MaskBinding> pendingInput = null;
            int seenRevision = -1; long nextAutomatic = 0; string lastError = null;
            try
            {
                while (true)
                {
                    int id, generation, target; bool frozen, form, quit, explicitTarget;
                    lock (gate) { id = processId; generation = revision; target = storedMasks; frozen = freeze; form = formOnHit; quit = disposed; explicitTarget = manualTarget; }
                    try
                    {
                        if (generation != seenRevision)
                        {
                            if (patch != null && patch.Pending) { patch.Cancel(); if (patch.Pending) { wake.WaitOne(1); continue; } }
                            policy.Reset(); pendingInput = null; seenRevision = generation;
                        }
                        if (memory != null && (memory.Process.HasExited || memory.Process.Id != id || quit))
                        { Close(ref patch, ref memory); previousScene = null; policy.Reset(); Publish(MaskSnapshot.Unavailable); }
                        if (id == 0 || quit)
                        {
                            Close(ref patch, ref memory); CompleteStop();
                            if (quit) break;
                            wake.WaitOne(); continue;
                        }
                        if (memory == null)
                        {
                            using (var targetProcess = Process.GetProcessById(id)) memory = new MaskMemory(targetProcess);
                        }
                        var state = memory.Snapshot();
                        if (state.Binding == null || !state.Binding.SameScene(previousScene))
                        {
                            lock (gate)
                            {
                                if (generation == revision) manualTarget = false;
                                if (pausedTarget.HasValue && (state.Binding == null || !pausedBinding.SameScene(state.Binding))) ClearPausedTarget();
                            }
                            explicitTarget = false;
                            if (pendingInput != null && (state.Binding == null || !state.Binding.SameScene(pendingInput.Item2))) pendingInput = null;
                            // Inputs captured in a previous scene never move to the next one.
                            lock (gate)
                            {
                                while (inputs.Count > 0 && (state.Binding == null || !state.Binding.SameScene(inputs.Peek().Item2))) inputs.Dequeue();
                            }
                            previousScene = state.Binding; policy.Reset();
                        }
                        Publish(state);
                        if (patch != null && patch.Pending)
                        {
                            int completion = state.Ready ? patch.Poll() : patch.Cancel();
                            if (completion == 1) pendingInput = null;
                            if (completion == 3) throw new InvalidOperationException("The game could not construct the mask event.");
                            if (patch.Pending) { wake.WaitOne(1); continue; }
                            // Read the post-event state before selecting the next command.
                            state = memory.Snapshot(); Publish(state);
                        }
                        if (state.Paused && state.CanConfigure)
                        {
                            // Keep the selection and freeze policy; the native dispatcher still guards pause == 0.
                            wake.WaitOne(8); continue;
                        }
                        if (!state.Ready)
                        {
                            lock (gate) { inputs.Clear(); ClearPausedTarget(); if (generation == revision) manualTarget = false; }
                            pendingInput = null;
                            policy.DesiredCount(state.Count, state.Invincible, false, frozen, target, form);
                            wake.WaitOne(8); continue;
                        }
                        int direction = 0; bool manual = false;
                        lock (gate)
                        {
                            if (generation != revision) continue;
                            if (explicitTarget && state.Count == target) { explicitTarget = false; manualTarget = false; }
                            if (pausedTarget.HasValue)
                            {
                                if (!pausedBinding.SameScene(state.Binding) || state.Count == pausedTarget.Value) ClearPausedTarget();
                                else { direction = Math.Sign(pausedTarget.Value - state.Count); manual = true; policy.ManualChange(); }
                            }
                            while (!manual && (pendingInput != null || inputs.Count > 0))
                            {
                                var input = pendingInput ?? inputs.Dequeue();
                                pendingInput = null;
                                if (!state.Binding.SameScene(input.Item2)) continue;
                                if (input.Item1 > 0 ? !state.Invincible && state.Count < 3 : state.Count > 0)
                                { pendingInput = input; direction = input.Item1; manual = true; policy.ManualChange(); break; }
                            }
                        }
                        if (!manual)
                        {
                            int desired = explicitTarget ? target : policy.DesiredCount(state.Count, state.Invincible, true, frozen, target, form);
                            // Automatic downward corrections must not abort the game's post-hit timer.
                            if (!explicitTarget && state.DamagedInvulnerable && desired < state.Count) desired = state.Count;
                            if (state.Count != desired && (explicitTarget || Stopwatch.GetTimestamp() >= nextAutomatic))
                                direction = state.Count < desired ? 1 : -1;
                        }
                        if (direction != 0)
                        {
                            if (patch == null) patch = new MaskCommandPatch(memory);
                            if (!manual && !explicitTarget && direction > 0 && state.Count < 2) patch.Restore(state);
                            else patch.Send(state, direction);
                            if (!manual && !explicitTarget) nextAutomatic = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 30;
                        }
                        lastError = null;
                    }
                    catch (Exception ex)
                    {
                        Publish(MaskSnapshot.Unavailable); policy.Reset(); previousScene = null;
                        pendingInput = null;
                        lock (gate) { inputs.Clear(); ClearPausedTarget(); }
                        if (lastError != ex.Message) { HelperLog.Error("Mask events", ex); lastError = ex.Message; }
                        // Keep an in-flight dispatcher owned until it has completed or been cancelled.
                        if (patch != null)
                        {
                            try { patch.Cancel(); }
                            catch (Exception cancelError) { HelperLog.Error("Cancel mask event", cancelError); }
                        }
                        if (id == 0 || quit) { CompleteStop(ex); if (quit) break; }
                    }
                    wake.WaitOne(patch?.Pending == true ? 1 : 8);
                }
            }
            finally
            {
                try { Close(ref patch, ref memory); CompleteStop(); }
                catch (Exception ex) { CompleteStop(ex); HelperLog.Error("Restore mask dispatcher", ex); }
                wake.Dispose();
            }
        }
        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; processId = 0; inputs.Clear(); ClearPausedTarget(); }
            wake.Set();
        }
    }
}
