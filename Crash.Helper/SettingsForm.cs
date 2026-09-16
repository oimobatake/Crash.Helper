using System;
using System.Drawing;
using System.Windows.Forms;
using Crash.Helper.Controls;

namespace Crash.Helper
{
    public sealed class SettingsForm : Form
    {
        public SettingsForm(HelperSettings settings, HotkeyControl hotkeys)
        {
            Text = "CrashHelper Settings";
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), Dock = DockStyle.Fill };
            layout.Controls.Add(new Label { Text = "Steam application", AutoSize = true });
            var pathRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            var path = new TextBox { Width = 365, Text = settings.SteamPath };
            var browse = new Button { Text = "Browse...", AutoSize = true };
            pathRow.Controls.Add(path);
            pathRow.Controls.Add(browse);
            layout.Controls.Add(pathRow);
            layout.Controls.Add(hotkeys);
            var save = new Button { Text = "Save", AutoSize = true };
            layout.Controls.Add(save);
            Controls.Add(layout);
            path.TextChanged += (s, e) => settings.SteamPath = path.Text.Trim();
            browse.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog { Filter = "Applications (*.exe)|*.exe", FileName = "steam.exe", Title = "Select Steam application" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) path.Text = dialog.FileName;
            };
            save.Click += (s, e) => Close();
            FormClosing += (s, e) => { if (!SaveSettings(settings)) e.Cancel = true; };
            // The hotkey control belongs to the main form and must outlive this window.
            FormClosed += (s, e) => layout.Controls.Remove(hotkeys);
        }

        private bool SaveSettings(HelperSettings settings)
        {
            try { settings.Save(); return true; }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save settings: " + ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
