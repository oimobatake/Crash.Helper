using System;
using System.Drawing;
using Crash.Helper.Input;
using Crash.Helper.Launcher;
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
        private readonly LocationControl locationControl;
        private readonly ProcessControl processControl;
        private readonly Timer refreshTimer;
        private readonly HelperSettings settings;
        private readonly Button settingsButton;
        private readonly HotkeyManager hotkeyManager;
        private readonly SteamGameLauncher launcher;

        public HelperForm() : this(new CrashMemory()) { }

        internal HelperForm(CrashMemory memory)
        {
            InitializeComponent();
            try { settings = HelperSettings.Load(); }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load settings: " + ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                settings = new HelperSettings();
            }
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            dataControl = new DataControl(memory);
            levelSelector = new LevelSelectorControl(dataControl);
            launcher = new SteamGameLauncher(settings);
            levelSelector.LaunchRequested += LaunchGame;
            locationControl = new LocationControl(memory) { Enabled = false };
            hotkeyManager = new HotkeyManager(HelperHotkeyActions.Create(memory, dataControl, levelSelector, locationControl), settings,
                () => memory.ProcessHooked, action => { if (!IsDisposed && IsHandleCreated) BeginInvoke(action); });
            processControl = new ProcessControl(memory, this);
            settingsButton = new Button { Text = "Settings", AutoSize = true };
            settingsButton.Click += (s, e) =>
            {
                using (var window = new SettingsForm(settings, hotkeyManager, memory, () => dataControl.Enabled)) window.ShowDialog(this);
                hotkeyManager.SetEditing(false);
            };
            flowLayoutPanel.Controls.Add(processControl);
            flowLayoutPanel.Controls.Add(dataControl);
            flowLayoutPanel.Controls.Add(levelSelector);
            flowLayoutPanel.Controls.Add(locationControl);
            var settingsRow = new Panel { Height = settingsButton.PreferredSize.Height, Width = levelSelector.LaunchButtonRight, Margin = new Padding(levelSelector.Margin.Left, 3, levelSelector.Margin.Right, 3) };
            settingsButton.AutoSize = false;
            settingsButton.Size = settingsButton.PreferredSize;
            settingsButton.Location = new Point(settingsRow.Width - settingsButton.Width, 0);
            settingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            settingsRow.Controls.Add(settingsButton);
            flowLayoutPanel.Controls.Add(settingsRow);
            levelSelector.Layout += (s, e) => settingsRow.Width = levelSelector.LaunchButtonRight;
            flowLayoutPanel.Height--;
            refreshTimer = new Timer { Interval = 100 };
            refreshTimer.Tick += (s, e) => RefreshHelper();
            processControl.Rescan();
        }

        public void ApplyAvailability(bool helperEnabled, bool ready)
        {
            dataControl.Enabled = ready;
            locationControl.Enabled = ready;
            locationControl.RefreshValues();
            levelSelector.ApplyAvailability(helperEnabled, ready);
            settingsButton.Enabled = helperEnabled;
            hotkeyManager.SetReady(ready);
            if (ready) refreshTimer.Start();
            else refreshTimer.Stop();
        }

        private void LaunchGame(string map)
        {
            try { processControl.PrepareForLaunch(); } catch { }
            try { launcher.Launch(map); }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to launch Steam. Tried: {settings.SteamPath}\nError: {ex.Message}", "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshHelper()
        {
            if (!memory.HookProcess()) { processControl.OnUnhook(); return; }
            memory.Refresh();
            locationControl.RefreshValues();
        }

        private void HelperForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ApplyAvailability(false, false);
            refreshTimer.Dispose();

            hotkeyManager.Dispose();
        }
    }
}
