using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Crash.Helper.Memory.Position
{
    internal sealed class PositionPatchService : IDisposable
    {
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private PositionPatch patch;
        private int generation, offset;
        private volatile bool fadeWriteDisabled;
        internal void SetFadeWriteDisabled(bool value) => fadeWriteDisabled = value;

        internal Task ConfigureAsync(Process process, int codeOffset, bool enabled, GameMemoryProfile profile = null)
        {
            int version = Interlocked.Increment(ref generation);
            return Task.Run(async () =>
            {
                await gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (version != Volatile.Read(ref generation)) return;
                    if (patch != null && (process == null || patch.HasExited || patch.ProcessId != process.Id || offset != codeOffset))
                    {
                        patch.Dispose(); patch = null;
                    }
                    if (!enabled || process == null) { patch?.SetEnabled(false); return; }
                    if (patch == null)
                    {
                        patch = new PositionPatch(process, process.MainModule.BaseAddress.ToInt64() + codeOffset);
                        offset = codeOffset;
                    }
                    patch.SetEnabled(!fadeWriteDisabled && (profile == null || !FadeMemory.SuspendPositionFreeze(process, profile)));
                    if (profile != null) MonitorLoading(process, profile, version);
                }
                finally { gate.Release(); }
            });
        }

        private async void MonitorLoading(Process process, GameMemoryProfile profile, int version)
        {
            bool loadingSeen = false;
            try
            {
                while (version == Volatile.Read(ref generation))
                {
                    await Task.Delay(8).ConfigureAwait(false);
                    await gate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        if (version != Volatile.Read(ref generation) || patch == null || patch.HasExited) return;
                        loadingSeen |= LoadingMemory.Read(process, profile.Loading);
                        patch.SetEnabled(!loadingSeen && !fadeWriteDisabled && !FadeMemory.SuspendPositionFreeze(process, profile));
                    }
                    finally { gate.Release(); }
                }
            }
            catch (Exception ex) { HelperLog.Error("Suspend player position instructions during fade or loading", ex); }
        }

        public void Dispose() => ConfigureAsync(null, 0, false).GetAwaiter().GetResult();
    }
}
