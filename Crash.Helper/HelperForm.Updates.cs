using System.Threading.Tasks;

namespace Crash.Helper
{
    public partial class HelperForm
    {
        internal bool UpdateSuspended { get; private set; }
        private bool updateLoadingSeen;

        internal async Task SuspendForUpdateAsync()
        {
            UpdateSuspended = true;
            updateLoadingSeen = false;
            hotkeyManager.SetReady(false);
            refreshTimer.Stop();
            loadingTimer.Stop();
            // Stop workers and restore injected instructions before showing a modal dialog.
            var tasks = new[] { dataControl.SuspendForUpdateAsync(), positionControl.SuspendForUpdateAsync(),
                cameraControl.SuspendForUpdateAsync(), othersControl.SuspendForUpdateAsync() };
            flowLayoutPanel.Enabled = false;
            // Observe loading without writing, so a completed load still clears old controls.
            if (advancedAvailable) loadingTimer.Start();
            await Task.WhenAll(tasks);
        }

        internal void ResumeAfterUpdate()
        {
            if (!UpdateSuspended || IsDisposed) return;
            if (updateLoadingSeen)
            {
                positionControl.SetLoading(true);
                cameraControl.SetLoading(true);
            }
            // Restore availability first, so an exited/replaced game is never resumed blindly.
            flowLayoutPanel.Enabled = true;
            UpdateSuspended = false;
            processControl.Rescan();
            dataControl.ResumeAfterUpdate();
            positionControl.ResumeAfterUpdate();
            cameraControl.ResumeAfterUpdate();
            othersControl.ResumeAfterUpdate();
        }
    }
}
