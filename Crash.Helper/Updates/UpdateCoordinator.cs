using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Crash.Helper.Updates
{
    internal sealed class UpdateCoordinator : IDisposable
    {
        private readonly HelperForm helper;
        private readonly HttpClient client;
        private readonly string stagingRoot;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        internal UpdatePackage Pending { get; private set; }

        internal UpdateCoordinator(HelperForm helper, HttpClient client = null, string stagingRoot = null)
        {
            this.helper = helper;
            this.client = client ?? ReleaseUpdater.CreateClient();
            this.stagingRoot = stagingRoot ?? UpdateInstaller.StagingRoot;
            helper.Shown += CheckOnStartup;
            helper.FormClosed += OnClosed;
        }

        private void OnClosed(object sender, System.Windows.Forms.FormClosedEventArgs e) => lifetime.Cancel();

        private async void CheckOnStartup(object sender, EventArgs e)
        {
            helper.Shown -= CheckOnStartup;
            if (!helper.AutoUpdateEnabled) return;
            bool suspended = false;
            try
            {
                var updater = new ReleaseUpdater(client);
                string executable = typeof(Program).Assembly.Location;
                var release = await updater.CheckAsync(executable, lifetime.Token);
                if (release == null) return;
                // Do not discard edits in an open Settings dialog or interrupt an existing close.
                while (!helper.IsDisposed && helper.AutoUpdateEnabled && (helper.OwnedForms.Any(form => form.Visible) || helper.UpdateClosePending))
                    await Task.Delay(500, lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                if (helper.IsDisposed || !helper.AutoUpdateEnabled) return;
                suspended = true;
                await helper.SuspendForUpdateAsync();
                lifetime.Token.ThrowIfCancellationRequested();
                if (!helper.AutoUpdateEnabled) return;
                using (var dialog = new UpdateDialog(async () =>
                {
                    var package = await updater.DownloadAsync(release, executable, stagingRoot, lifetime.Token);
                    Pending = package;
                    try
                    {
                        helper.Close();
                        while (!helper.IsDisposed && helper.UpdateClosePending)
                            await Task.Delay(50);
                        if (!helper.IsDisposed)
                            throw new InvalidOperationException("The helper could not finish restoring its game hooks.");
                    }
                    catch { Pending = null; throw; }
                })) dialog.ShowDialog(helper);
            }
            catch (OperationCanceledException ex)
            {
                if (!lifetime.IsCancellationRequested) HelperLog.Error("Check for updates timed out", ex);
            }
            catch (Exception ex) { HelperLog.Error("Check for updates", ex); }
            finally
            {
                if (suspended && !helper.IsDisposed && !helper.UpdateClosePending) helper.ResumeAfterUpdate();
            }
        }

        public void Dispose()
        {
            helper.Shown -= CheckOnStartup;
            helper.FormClosed -= OnClosed;
            lifetime.Cancel();
            client.Dispose();
            // The outstanding async continuation can still inspect the cancellation source.
        }
    }
}
