using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Crash.Helper.Memory
{
    internal sealed class FadePatchService : IDisposable
    {
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private FadePatch patch;
        private int generation, offset;

        internal Task ConfigureAsync(Process process, GameMemoryProfile profile, bool enabled)
        {
            int codeOffset = profile?.FadeCodeOffset ?? 0;
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
                    if (!enabled || process == null || profile == null) { patch?.SetEnabled(false); return; }
                    if (patch == null)
                    {
                        patch = new FadePatch(process, process.MainModule.BaseAddress.ToInt64() + codeOffset, profile.FadeOriginal);
                        offset = codeOffset;
                    }
                    patch.SetEnabled(true);
                }
                finally { gate.Release(); }
            });
        }

        public void Dispose() => ConfigureAsync(null, null, false).GetAwaiter().GetResult();
    }
}
