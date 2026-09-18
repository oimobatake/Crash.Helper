using System;
using System.Drawing;
using System.Windows.Forms;
using Crash.Helper.Controls;
using Crash.Helper.Input;
using Crash.Helper.Memory;

namespace Crash.Helper
{
    public sealed class SettingsForm : Form
    {
        private readonly HelperSettings settings;
        private readonly HelperSettings draft;
        private readonly HotkeyManager manager;
        private readonly TextBox path;
        private readonly Button save;
        private readonly TabControl tabs;
        private string pathBeforeEdit;

        internal SettingsForm(HelperSettings settings, HotkeyManager manager, CrashMemory memory, Func<bool> canEditGame, bool helperEnabled)
        {
            this.settings = settings;
            this.manager = manager;
            draft = settings.Clone();
            Text = "Crash Helper Settings";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, Math.Min(410, Screen.PrimaryScreen.WorkingArea.Height - 80));
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            tabs = new TabControl { Dock = DockStyle.Fill, TabIndex = 0 };
            var gamePage = new TabPage("GameFlag") { Padding = new Padding(10), AutoScroll = true };
            var helperPage = new TabPage("CrashHelper") { Padding = new Padding(8), AutoScroll = true };
            tabs.TabPages.Add(gamePage);
            tabs.TabPages.Add(helperPage);
            gamePage.Controls.Add(new GameFlagControl(memory, canEditGame) { Dock = DockStyle.Top });
            var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Top };
            layout.Controls.Add(new Label { Text = "Steam application", AutoSize = true });
            var pathRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            path = new TextBox { Width = 300, Text = draft.SteamPath };
            var browse = new Button { Text = "Browse...", AutoSize = true };
            pathRow.Controls.Add(path);
            pathRow.Controls.Add(browse);
            layout.Controls.Add(pathRow);
            layout.Controls.Add(new HotkeyControl(manager, draft) { Enabled = helperEnabled });
            helperPage.Controls.Add(layout);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
            save = new Button { Text = "Save", Size = new Size(75, 25), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            footer.Controls.Add(save);
            footer.Resize += (s, e) => save.Location = new Point(footer.ClientSize.Width - footer.Padding.Right - save.Width, 8);
            Controls.Add(tabs);
            Controls.Add(footer);
            path.Enter += (s, e) => { pathBeforeEdit = path.Text; manager.SetEditing(true); };
            path.Leave += (s, e) => { FinishPathEdit(); manager.SetEditing(false); };
            browse.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog { Filter = "Applications (*.exe)|*.exe", FileName = "steam.exe", Title = "Select Steam application" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) { path.Text = dialog.FileName; draft.SteamPath = dialog.FileName; }
            };
            save.Click += (s, e) => SaveSettings();
            WireFocusClearing(this);
            Shown += (s, e) => { tabs.Focus(); path.Select(0, 0); };
            tabs.SelectedIndexChanged += (s, e) => { ActiveControl = tabs; tabs.Focus(); path.Select(0, 0); };
            FormClosed += (s, e) => manager.SetEditing(false);
        }

        private void FinishPathEdit()
        {
            if (string.IsNullOrWhiteSpace(path.Text) || path.Text == pathBeforeEdit)
                path.Text = pathBeforeEdit ?? draft.SteamPath;
            draft.SteamPath = path.Text.Trim();
            path.Select(0, 0);
        }

        private void WireFocusClearing(Control root)
        {
            // Clearing focus during a button press cancels its mouse capture and click.
            // Only passive surfaces should clear the active editor; inputs handle focus normally.
            if (root is Form || root is Panel || root is GroupBox || root is Label || root is UserControl)
                root.MouseDown += (s, e) => { ActiveControl = null; };
            foreach (Control child in root.Controls)
            {
                WireFocusClearing(child);
            }
        }

        private void SaveSettings()
        {
            FinishPathEdit();
            try
            {
                draft.Save();
                settings.CopyFrom(draft);
                manager.ReloadSettings();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save settings: " + ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
