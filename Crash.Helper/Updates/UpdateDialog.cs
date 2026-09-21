using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Crash.Helper.Updates
{
    internal sealed class UpdateDialog : Form
    {
        private readonly Label message;
        private readonly FlowLayoutPanel buttons;
        private readonly Button yes, no;
        private bool busy;

        internal UpdateDialog(Func<Task> update = null)
        {
            Text = "Crash Helper";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(450, 135);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = true;
            message = new Label { AutoSize = false, Dock = DockStyle.Fill, Padding = new Padding(20), TextAlign = ContentAlignment.MiddleLeft,
                Text = "A new version of Crash Helper is available. Would you like to update?" };
            buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            no = new Button { Text = "No", DialogResult = DialogResult.No, Width = 85 };
            yes = new Button { Text = "Yes", Width = 85 };
            buttons.Controls.Add(no);
            buttons.Controls.Add(yes);
            Controls.Add(message);
            Controls.Add(buttons);
            AcceptButton = yes;
            CancelButton = no;
            Shown += (s, e) => { Activate(); BringToFront(); };
            yes.Click += async (s, e) =>
            {
                ShowProgress();
                try { await update(); }
                catch (Exception ex)
                {
                    HelperLog.Error("Update Crash Helper", ex);
                    if (IsDisposed) return;
                    busy = false;
                    ControlBox = true;
                    message.Text = "Failed to update. Crash Helper can still be used. Please check the log for more details.";
                    yes.Visible = false;
                    no.Text = "Close";
                    buttons.Visible = true;
                    return;
                }
                busy = false;
                if (!IsDisposed) { DialogResult = DialogResult.OK; Close(); }
            };
            if (update == null) ShowProgress();
        }

        private void ShowProgress()
        {
            busy = true;
            ControlBox = false;
            buttons.Visible = false;
            AcceptButton = CancelButton = null;
            message.Text = "Updating. Please wait a moment...";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            base.OnFormClosing(e);
        }

        internal void Complete() { busy = false; Close(); }
    }
}
