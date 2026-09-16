using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Crash.Helper.Memory.LevelLock
{
    internal sealed class LevelLockService
    {
        private readonly object sync = new object();
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource pending;
        private LevelLockPatch patch;

        internal Task SetAsync(Process process, string name)
        {
            CancellationTokenSource request;
            lock (sync)
            {
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
                    if (process == null || name == null) { patch?.Disable(); return; }
                    if (patch != null && (patch.HasExited || patch.ProcessId != process.Id)) { patch.Dispose(); patch = null; }
                    if (patch == null) patch = new LevelLockPatch(process);
                    try { patch.Enable(name, request.Token); }
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

        internal async Task ShutdownAsync()
        {
            await SetAsync(null, null).ConfigureAwait(false);
            await gate.WaitAsync().ConfigureAwait(false);
            try { patch?.Dispose(); patch = null; }
            finally { gate.Release(); }
        }
    }
}
