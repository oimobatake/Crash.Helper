using System;
using System.Windows.Forms;
using Crash.Helper.Controls;
using Crash.Helper.Memory;

namespace Crash.Helper
{
    public partial class HelperForm : Form
    {
        private readonly CrashMemory memory;
        private readonly DataControl dataControl;
        private readonly LevelSelectorControl levelSelector;
        private readonly HotkeyControl hotkeyControl;
        private readonly ProcessControl processControl;
        private readonly Timer refreshTimer;
        private readonly HelperSettings settings;
        private readonly Button settingsButton;

        public HelperForm()
        {
            InitializeComponent();
            try { settings = HelperSettings.Load(); }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load settings: " + ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                settings = new HelperSettings();
            }
            memory = new CrashMemory();
            dataControl = new DataControl(memory);
            levelSelector = new LevelSelectorControl(memory, dataControl) { SteamPath = settings.SteamPath };
            hotkeyControl = new HotkeyControl(memory, dataControl, levelSelector, settings);
            processControl = new ProcessControl(memory, dataControl, hotkeyControl, this);
            settingsButton = new Button { Text = "Settings", AutoSize = true };
            settingsButton.Click += (s, e) =>
            {
                using (var window = new SettingsForm(settings, hotkeyControl)) window.ShowDialog(this);
                levelSelector.SteamPath = settings.SteamPath;
                hotkeyControl.EndEditing();
            };
            flowLayoutPanel.Controls.Add(processControl);
            flowLayoutPanel.Controls.Add(dataControl);
            flowLayoutPanel.Controls.Add(levelSelector);
            flowLayoutPanel.Controls.Add(settingsButton);
            flowLayoutPanel.Height--;
            refreshTimer = new Timer { Interval = 100 };
            refreshTimer.Tick += (s, e) => RefreshHelper();
            processControl.Rescan();
        }

        public void ApplyAvailability(bool helperEnabled, bool ready)
        {
            dataControl.Enabled = ready;
            levelSelector.ApplyAvailability(helperEnabled, ready);
            settingsButton.Enabled = helperEnabled;
            hotkeyControl.SetReady(ready);
            if (ready) refreshTimer.Start();
            else refreshTimer.Stop();
        }

        public void PrepareHelperForLaunch() { processControl.PrepareForLaunch(); }

        private void RefreshHelper()
        {
            if (!memory.HookProcess()) { processControl.OnUnhook(); return; }
            memory.Refresh();
        }

        private void HelperForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ApplyAvailability(false, false);
            refreshTimer.Dispose();
            hotkeyControl.Dispose();
        }
    }
}
