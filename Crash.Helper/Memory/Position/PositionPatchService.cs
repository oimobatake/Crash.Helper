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

        internal Task ConfigureAsync(Process process, int codeOffset, bool enabled)
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
                    patch.SetEnabled(true);
                }
                finally { gate.Release(); }
            });
        }

        public void Dispose() => ConfigureAsync(null, 0, false).GetAwaiter().GetResult();
    }
}
