using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Memory;

namespace Crash.Helper.Controls
{
    internal sealed class OthersControl : GroupBox
    {
        private readonly CheckBox disableFade = new CheckBox { Text = "Disable fade write", AutoSize = true, Location = new Point(12, 22) };
        private readonly FadePatchService service = new FadePatchService();
        private Process process;
        private GameMemoryProfile profile;
        private bool closing;
        private bool updateSuspended;
        internal event Action<bool> FadeWriteDisabledChanged;

        internal OthersControl()
        {
            Text = "Others"; Size = new Size(285, 55); Margin = new Padding(0, 5, 0, 0); Enabled = false;
            Controls.Add(disableFade);
            disableFade.CheckedChanged += (s, e) =>
            {
                FadeWriteDisabledChanged?.Invoke(disableFade.Checked);
                Configure();
            };
        }

        internal void SetAvailability(Process target, GameMemoryProfile memoryProfile)
        {
            process = target; profile = memoryProfile;
            Enabled = !closing && process != null && profile != null;
            Configure();
        }

        internal void ResetControls() => disableFade.Checked = false;

        internal void ToggleFade()
        {
            if (Enabled && !closing) disableFade.Checked = !disableFade.Checked;
        }

        private async void Configure()
        {
            if (closing || updateSuspended) return;
            try { await service.ConfigureAsync(process, profile, Enabled && disableFade.Checked); }
            catch (Exception ex) { HelperLog.Error("Configure fade instructions", ex); }
        }

        internal Task ShutdownAsync()
        {
            closing = true;
            return service.ConfigureAsync(null, null, false);
        }

        internal Task SuspendForUpdateAsync()
        {
            updateSuspended = true;
            return service.ConfigureAsync(null, null, false);
        }

        internal void ResumeAfterUpdate() { updateSuspended = false; Configure(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { closing = true; service.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
