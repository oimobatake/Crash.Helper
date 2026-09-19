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
        private readonly CameraControl cameraControl;
        private readonly GroupBox cameraBox;
        private readonly ProcessControl processControl;
        private readonly Timer refreshTimer;
        private readonly HelperSettings settings;
        private readonly Button settingsButton;
        private readonly CheckBox advancedButton;
        private readonly FlowLayoutPanel rightColumn;
        private readonly HotkeyManager hotkeyManager;
        private readonly SteamGameLauncher launcher;
        private bool levelLockCleanupComplete;
        private bool levelLockCleanupPending;
        private bool updatingAdvancedButton;

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
            cameraControl = new CameraControl();
            hotkeyManager = new HotkeyManager(HelperHotkeyActions.Create(memory, dataControl, levelSelector, positionControl, cameraControl), settings,
                () => memory.ProcessHooked, action => { if (!IsDisposed && IsHandleCreated) BeginInvoke(action); },
                () => ForegroundApplication.IsGameOrHelper(memory.LoadMap.Process));
            cameraControl.EditingChanged += hotkeyManager.SetEditing;
            positionControl.EditingChanged += hotkeyManager.SetEditing;
            hotkeyManager.PositionMovementChanged += positionControl.SetMovement;
            hotkeyManager.PositionSpeedBoostChanged += positionControl.SetSpeedBoost;
            hotkeyManager.CameraMovementChanged += cameraControl.SetMovement;
            hotkeyManager.CameraSpeedBoostChanged += cameraControl.SetSpeedBoost;
            Deactivate += (s, e) => cameraControl.EndEditing();
            processControl = new ProcessControl(memory, this);
            settingsButton = new Button { Text = "Settings", AutoSize = true };
            settingsButton.Click += (s, e) =>
            {
                using (var window = new SettingsForm(settings, hotkeyManager, memory, () => dataControl.Enabled, processControl.HelperEnabled)) window.ShowDialog(this);
                hotkeyManager.SetEditing(false);
            };
            advancedButton = new CheckBox
            {
                Text = "Advanced Controls", Appearance = Appearance.Button, AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter, Checked = settings.AdvancedControlsEnabled
            };
            flowLayoutPanel.FlowDirection = FlowDirection.LeftToRight;
            flowLayoutPanel.WrapContents = false;
            var leftColumn = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty };
            rightColumn = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(12, 0, 0, 0), Visible = settings.AdvancedControlsEnabled };
            flowLayoutPanel.Controls.Add(leftColumn);
            flowLayoutPanel.Controls.Add(rightColumn);
            leftColumn.Controls.Add(processControl);
            leftColumn.Controls.Add(dataControl);
            leftColumn.Controls.Add(levelSelector);
            positionBox = new GroupBox { Text = "Position", Size = new Size(285, positionControl.Height + 26), Margin = Padding.Empty, Enabled = false };
            positionControl.Location = new Point(7, 19);
            positionControl.Margin = Padding.Empty;
            positionBox.Controls.Add(positionControl);
            rightColumn.Controls.Add(positionBox);
            cameraBox = new GroupBox { Text = "Camera", Size = new Size(285, 250), Margin = new Padding(0, 5, 0, 0), Enabled = false };
            cameraControl.Location = new Point(7, 19);
            cameraControl.Margin = Padding.Empty;
            cameraBox.Controls.Add(cameraControl);
            cameraControl.SizeChanged += (s, e) => cameraBox.Height = cameraControl.Height + 26;
            cameraBox.Height = cameraControl.Height + 26;
            rightColumn.Controls.Add(cameraBox);
            var settingsRow = new Panel { Height = Math.Max(settingsButton.PreferredSize.Height, advancedButton.PreferredSize.Height), Width = levelSelector.Width, Margin = new Padding(levelSelector.Margin.Left, 3, levelSelector.Margin.Right, 3) };
            settingsButton.AutoSize = false;
            settingsButton.Size = settingsButton.PreferredSize;
            settingsRow.Controls.Add(settingsButton);
            settingsRow.Controls.Add(advancedButton);
            settingsRow.Layout += (s, e) =>
            {
                int width = settingsButton.Width + 8 + advancedButton.Width;
                settingsButton.Location = new Point((settingsRow.ClientSize.Width - width) / 2, 0);
                advancedButton.Location = new Point(settingsButton.Right + 8, 0);
            };
            leftColumn.Controls.Add(settingsRow);
            levelSelector.Layout += (s, e) => settingsRow.Width = levelSelector.Width;
            flowLayoutPanel.Height--;
            refreshTimer = new Timer { Interval = 100 };
            refreshTimer.Tick += (s, e) => RefreshHelper();
            advancedButton.CheckedChanged += (s, e) => ToggleAdvancedControls();
            hotkeyManager.StatusChanged += (s, e) => RefreshCameraInput();
            RefreshCameraInput();
            processControl.Rescan();
        }

        private void RefreshCameraInput()
        {
            cameraControl.ApplySettings(settings, hotkeyManager.Hotkeys);
            cameraControl.SetInputEnabled(hotkeyManager.CanUseCameraInput);
            positionControl.ApplySettings(settings, cameraControl.GetViewOrientation);
            positionControl.SetInputEnabled(hotkeyManager.CanUseCameraInput);
        }

        private void ToggleAdvancedControls()
        {
            if (updatingAdvancedButton) return;
            try
            {
                HelperSettings.SaveAdvancedControls(advancedButton.Checked);
                settings.AdvancedControlsEnabled = advancedButton.Checked;
            }
            catch (Exception ex)
            {
                updatingAdvancedButton = true;
                advancedButton.Checked = settings.AdvancedControlsEnabled;
                updatingAdvancedButton = false;
                HelperLog.Error("Save Advanced Controls", ex);
                MessageBox.Show(this, "Could not save Advanced Controls.\n" + ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            cameraControl.EndEditing();
            processControl.Rescan();
            SuspendLayout();
            rightColumn.Visible = settings.AdvancedControlsEnabled;
            ResumeLayout(true);
            PerformLayout();
        }

        public void ApplyAvailability(bool helperEnabled, bool ready)
        {
            bool canConfigure = !levelLockCleanupPending && !levelLockCleanupComplete;
            ready = canConfigure && helperEnabled && ready && memory.IsSupportedVersion;
            dataControl.Enabled = ready;
            bool advancedReady = ready && settings.AdvancedControlsEnabled;
            positionBox.Enabled = advancedReady;
            positionControl.Enabled = advancedReady;
            positionControl.RefreshValues();
            cameraBox.Enabled = advancedReady && memory.Profile?.Camera != null;
            cameraControl.SetAvailability(advancedReady ? memory.LoadMap.Process : null, advancedReady ? memory.Profile?.Camera : null);
            levelSelector.ApplyAvailability(canConfigure, ready);
            settingsButton.Enabled = canConfigure;
            advancedButton.Enabled = canConfigure;
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
                    await System.Threading.Tasks.Task.WhenAll(cameraControl.ShutdownAsync(), positionControl.ShutdownAsync(), dataControl.ShutdownLevelLockAsync());
                    levelLockCleanupComplete = true;
                }
                catch (Exception ex)
                {
                    HelperLog.Error("Restore game hooks on close", ex);
                    MessageBox.Show(this, "Could not restore game hooks.\n" + ex.Message,
                        "Crash Helper", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
