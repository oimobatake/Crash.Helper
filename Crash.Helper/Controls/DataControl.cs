using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Memory;
using System.Threading;

namespace Crash.Helper.Controls
{
    public partial class DataControl : UserControl
    {
        // Event fired when map lock state changes. Parameter is storedMap or null when unlocked.
        public event EventHandler<string> MapLockChanged;

        // suppress checkbox event when setting Checked programmatically
        private bool suppressFreezeCheckboxEvent = false;
        // suppress restart checkbox programmatic event
        private bool suppressRestartCheckboxEvent = false;
        private System.Threading.Timer restartTimer;
        // when true, always keep Restart flag true in memory
        private bool restartForceEnabled = false;
        private CrashMemory memory;

        private int storedLives = 1;
        private volatile bool updateSuspended;
        private bool resumingUpdate;
        private string storedMap = null;

        private System.Threading.Timer livesFreezeTimer;
        // Internal flag for map freeze (UI checkbox is kept hidden)
        private bool freezeMapEnabled = false;

        public DataControl(CrashMemory memory)
        {
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));

            memory.Lives.OnValueChange += OnLivesChange;
            memory.Masks.OnValueChange += OnMasksChange;
            memory.LoadMap.OnValueChange += OnLoadMapChanged;
            memory.CurrentLevel.OnValueChange += (o, n) => SafeAction(RefreshLevelDisplay);
            //memory.Restart.OnValueChange += OnRestartChange;

            InitializeComponent();
            InitializeMaskOptions(false);
            InitializeSecretLevel();
            // wire freeze level checkbox handler
            freezeLevelCheckbox.CheckedChanged += freezeLevelCheckbox_CheckedChanged;
            damageMaskformCheckbox.Enabled = false;
        }

        public DataControl()
        {
            InitializeComponent();
            InitializeMaskOptions(false);
            InitializeSecretLevel();
            freezeLevelCheckbox.CheckedChanged += freezeLevelCheckbox_CheckedChanged;
            damageMaskformCheckbox.Enabled = false;
        }

        private void freezeLevelCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressFreezeCheckboxEvent) return;
            if (memory == null || !Enabled || !memory.ProcessHooked) return;

            if (freezeLevelCheckbox.Checked)
            {
                // The current level is a path, while the hook expects a loadmap command.
                var mapVal = GetMapCommand(memory.CurrentLevel.Read());
                if (!string.IsNullOrEmpty(mapVal)) SetMapLockAsync(mapVal, GetLevelDisplayName(mapVal), true, true);
                else ShowMapLock(nowMapLabels.Text, false, Color.Black);
            }
            else
            {
                StopMapLock();
            }
        }

        // Public accessor for whether map freeze is active
        public bool IsMapFrozen => freezeMapEnabled;

        public int Lives
        {
            set => RefreshLives(value);
        }

        public int Masks
        {
            set => RefreshMasks(value);
        }

        private void OnLivesChange(int oldLives, int newLives)
        {
            SafeAction(() => {
                if (!Enabled || !memory.ProcessHooked) return;
                if (freezeLivesCheckbox.Checked)
                {
                    memory.Lives.Write(storedLives);
                }
                else
                {
                    RefreshLives();
                }
            });
        }

        /*
        private void OnRestartChange(byte oldValue, byte newValue)
        {
            // Update UI to reflect actual memory value (do not write back here)
            SafeAction(() => {
                try
                {
                    suppressRestartCheckboxEvent = true;
                    displayRestartCheckBox.Checked = newValue != 0;
                }
                finally { suppressRestartCheckboxEvent = false; }
            });
        }
        */

        private void livesUpButton_Click(object sender, EventArgs e)
        {
            int newLives = memory.Lives.Read() + 1;

            memory.Lives.Write(newLives);
            RefreshLives(newLives);
        }

        private void livesDownButton_Click(object sender, EventArgs e)
        {
            int newLives = memory.Lives.Read() - 1;

            memory.Lives.Write(newLives);
            RefreshLives(newLives);
        }

        private void freezeLivesCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (freezeLivesCheckbox.Checked)
            {
                FreezeLives();
                StartLivesFreeze();
            }
            else
            {
                StopLivesFreeze();
                storedLives = -1;
                livesLabel.ForeColor = Color.Black;
            }
        }

        /*
        private void displayRestartCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressRestartCheckboxEvent) return;

            // user toggled the checkbox: if checked, force Restart=true continuously; if unchecked, follow game state
            restartForceEnabled = displayRestartCheckBox.Checked;
            if (restartForceEnabled)
            {
                // immediately write true and start timer to maintain it
                try { memory.Restart.Write((byte)1); } catch { }
                StartRestart();
            }
            else
            {
                // stop forcing; stop timer and reflect current memory state in UI
                StopRestart();
                try
                {
                    suppressRestartCheckboxEvent = true;
                    displayRestartCheckBox.Checked = memory.Restart.Read() != 0;
                }
                catch { }
                finally { suppressRestartCheckboxEvent = false; }
            }
        }

        private void StartRestart()
        {
            StopRestart();
            restartTimer = new System.Threading.Timer(_ => {
                try { memory.Restart.Write((byte)1); } catch { }
            }, null, 0, 50);
        }
        */

        private void StopRestart()
        {
            restartTimer?.Dispose();
            restartTimer = null;
        }

        private void FreezeLives()
        {
            storedLives = memory.Lives.Read();
            livesLabel.ForeColor = Color.DodgerBlue;
        }

        /*
        private void DisplayRestart(bool restart)
        {
            memory.Restart.Write(restart ? (byte)1 : (byte)0);
        }
        */

        private void StartLivesFreeze()
        {
            StopLivesFreeze();
            livesFreezeTimer = new System.Threading.Timer(_ =>
            {
                if (memory == null || !Enabled || !memory.ProcessHooked) return;

                if (storedLives >= 0)
                {
                    memory.Lives.Write(storedLives);
                }
            }, null, 0, 10);
        }

        private void StopLivesFreeze()
        {
            livesFreezeTimer?.Dispose();
            livesFreezeTimer = null;
        }

        private void RefreshLives(int newLives = -1)
        {
            int lives = newLives != -1 ? newLives : memory.Lives.Read();

            SafeAction(() =>
            {
                livesDownButton.Enabled = lives > 0;
                livesUpButton.Enabled = lives < 999;
                livesLabel.Text = "Lives: " + lives;

                if (storedLives != -1)
                {
                    storedLives = lives;
                }
            });
        }

        private void SafeAction(Action action)
        {
            if (action == null) return;
            if (this.InvokeRequired)
            {
                try
                {
                    this.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    System.Diagnostics.Trace.WriteLine("[DataControl] SafeAction skipped: control disposed.");
                }
                catch (InvalidOperationException)
                {
                    System.Diagnostics.Trace.WriteLine("[DataControl] SafeAction skipped: invalid invoke state.");
                }
            }
            else
            {
                action();
            }
        }

        internal void RefreshGameSession()
        {
            if (memory == null || memory.ProcessHooked) return;
            // Only an exited game clears selections; temporarily disabling the helper preserves them.
            freezeLivesCheckbox.Checked = false;
            freezeMasksCheckbox.Checked = false;
            damageMaskformCheckbox.Checked = false;
            ClearLevelLockForExitedGame();
            if (!secretLevelCheckbox.Enabled && secretLevelCheckbox.Checked)
            {
                refreshingSecretCheckbox = true;
                try { secretLevelCheckbox.Checked = false; }
                finally { refreshingSecretCheckbox = false; }
            }
        }

        private void dataBox_EnabledChanged(object sender, EventArgs e)
        {
            if (memory == null || updateSuspended) return;
            ConfigureMaskEvents();

            if (Enabled)
            {
                if (!freezeMasksCheckbox.Checked)
                {
                    SyncStoredMasksWithCurrent();
                }

                RefreshLives(resumingUpdate && freezeLivesCheckbox.Checked ? storedLives : -1);
                RefreshMasks();
                RefreshLevelDisplay();

                if (freezeLivesCheckbox.Checked)
                {
                    // 再フック時は直前の保持値を優先して再適用する
                    if (storedLives < 0)
                    {
                        storedLives = memory.Lives.Read();
                    }
                    memory.Lives.Write(storedLives);
                    livesLabel.ForeColor = Color.DodgerBlue;
                    StartLivesFreeze();
                }
                if (freezeMasksCheckbox.Checked)
                {
                    // 再フック時は直前の保持値を再適用する
                    FreezeMasks();
                    damageMaskformCheckbox.Enabled = true;
                    StartMasksFreeze();
                }
                else
                {
                    damageMaskformCheckbox.Enabled = false;
                }
                if (freezeMapEnabled)
                {
                    // 再フック時は直前のstoredMapを優先して再適用する
                    if (!string.IsNullOrEmpty(storedMap))
                    {
                        var mapKey = LevelSelectorControl.Levels.Keys.FirstOrDefault(k => LevelSelectorControl.Levels[k] == storedMap);
                        SetMapLock(storedMap, mapKey, true);
                    }
                    else
                    {
                        // 保持値がない場合のみ現在Mapを保持してfreeze開始
                        freezeLevelCheckbox_CheckedChanged(this, EventArgs.Empty);
                    }
                }

                /*
                if (restartForceEnabled || displayRestartCheckBox.Checked)
                {
                    // 再フック時にRestart強制状態を復元
                    restartForceEnabled = true;
                    try { memory.Restart.Write((byte)1); } catch { }
                    StartRestart();
                }
                */
            }
            else
            {
                StopLivesFreeze();
                StopMasksFreeze();
                StopMapFreeze();
                StopRestart();
            }
        }

        public void SetLives(int value)
        {
            if (!Enabled || !memory.ProcessHooked) return;
            storedLives = value;
            memory.Lives.Write(value);
            RefreshLives(value);
        }

        public void ToggleFreezeLives() { if (Enabled) freezeLivesCheckbox.Checked = !freezeLivesCheckbox.Checked; }
        public void ToggleFreezeMasks() { if (Enabled) freezeMasksCheckbox.Checked = !freezeMasksCheckbox.Checked; }
        public void ToggleCurrentLevel() { if (Enabled) freezeLevelCheckbox.Checked = !freezeLevelCheckbox.Checked; }

        private void StopAllTimers()
        {
            updateSuspended = true;
            StopLivesFreeze();
            StopMasksFreeze();
            maskStatusTimer.Dispose();
            maskEvents.Dispose();
            _ = ShutdownLevelLockAsync();
            StopRestart();
            secretLevelTimer?.Dispose();
        }

        internal async Task SuspendForUpdateAsync()
        {
            updateSuspended = true;
            ++levelLockRevision;
            mapLockActive = false;
            secretLevelTimer.Stop();
            maskStatusTimer.Stop();
            var lives = livesFreezeTimer; livesFreezeTimer = null;
            var masks = masksFreezeTimer; masksFreezeTimer = null;
            var restart = restartTimer; restartTimer = null;
            await Task.WhenAll(DrainTimerAsync(lives), DrainTimerAsync(masks), DrainTimerAsync(restart), levelLock.SetAsync(null, null), maskEvents.StopAsync());
        }

        private static Task DrainTimerAsync(System.Threading.Timer timer)
        {
            if (timer == null) return Task.CompletedTask;
            return Task.Run(() =>
            {
                using (var drained = new ManualResetEvent(false))
                    if (timer.Dispose(drained)) drained.WaitOne();
            });
        }

        internal void ResumeAfterUpdate()
        {
            updateSuspended = false;
            resumingUpdate = true;
            try { dataBox_EnabledChanged(this, EventArgs.Empty); }
            finally { resumingUpdate = false; }
        }
    }
}
