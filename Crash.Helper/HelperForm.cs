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
        private readonly PositionControl positionControl;
        private readonly GroupBox positionBox;
        private readonly ProcessControl processControl;
        private readonly Timer refreshTimer;
        private readonly HelperSettings settings;
        private readonly Button settingsButton;
        private readonly HotkeyManager hotkeyManager;
        private readonly SteamGameLauncher launcher;
        private bool levelLockCleanupComplete;
        private bool levelLockCleanupPending;

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
            positionControl = new PositionControl(memory) { Enabled = false };
            hotkeyManager = new HotkeyManager(HelperHotkeyActions.Create(memory, dataControl, levelSelector, positionControl), settings,
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
            positionBox = new GroupBox { Text = "Position", Size = new Size(285, 138), Margin = new Padding(0, 5, 0, 0), Enabled = false };
            positionControl.Location = new Point(7, 19);
            positionControl.Margin = Padding.Empty;
            positionBox.Controls.Add(positionControl);
            flowLayoutPanel.Controls.Add(positionBox);
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
            ready = ready && memory.IsSupportedVersion;
            dataControl.Enabled = ready;
            positionBox.Enabled = ready;
            positionControl.Enabled = ready;
            positionControl.RefreshValues();
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
            positionControl.RefreshValues();
        }

        private async void HelperForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!levelLockCleanupComplete)
            {
                e.Cancel = true;
                if (levelLockCleanupPending) return;
                levelLockCleanupPending = true;
                ApplyAvailability(false, false);
                try
                {
                    await dataControl.ShutdownLevelLockAsync();
                    levelLockCleanupComplete = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not remove the level hook.\n" + ex.Message,
                        "Level lock", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { levelLockCleanupPending = false; }
                if (levelLockCleanupComplete) Close();
                return;
            }
            ApplyAvailability(false, false);
            refreshTimer.Dispose();

            hotkeyManager.Dispose();
        }
    }
}
