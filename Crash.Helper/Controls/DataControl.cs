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
        private sealed class MaskFreezeState
        {
            public int StoredMasks { get; private set; } = 0;
            public int RuntimeMasks { get; private set; } = 0;
            public bool IsMaskFormOnDamage { get; set; } = false;

            public void SetStoredMasks(int value)
            {
                if (value < 0) value = 0;
                if (value > 2) value = 2;
                StoredMasks = value;
            }

            public int ResolveTargetMasks(int currentMasks)
            {
                if (IsMaskFormOnDamage)
                {
                    if (currentMasks == 0)
                    {
                        RuntimeMasks = 0;
                        return 0;
                    }

                    // Keep in-game transition for hit animation/invincibility:
                    // 4 -> 3 -> 2 is allowed, and when it reaches 2 we restore to 4.
                    if (currentMasks == 3 || currentMasks == 4)
                    {
                        RuntimeMasks = currentMasks;
                        return currentMasks;
                    }

                    RuntimeMasks = 4;
                    return 4;
                }

                RuntimeMasks = StoredMasks;
                return StoredMasks;
            }

            public void SyncRuntimeMasks(int currentMasks)
            {
                RuntimeMasks = currentMasks;
            }
        }

        private readonly MaskFreezeState maskFreezeState = new MaskFreezeState();
        private bool suppressDamageMaskFormCheckboxEvent = false;

        public int StoredMasks
        {
            get { return maskFreezeState.StoredMasks; }
            set { maskFreezeState.SetStoredMasks(value); }
        }
        private string storedMap = null;

        private System.Threading.Timer livesFreezeTimer;
        private System.Threading.Timer masksFreezeTimer;
        // Internal flag for map freeze (UI checkbox is kept hidden)
        private bool freezeMapEnabled = false;

        public DataControl(CrashMemory memory)
        {
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));

            memory.Lives.OnValueChange += OnLivesChange;
            memory.Masks.OnValueChange += OnMasksChange;
            memory.LoadMap.OnValueChange += (o, n) => {
                SafeAction(() =>
                {
                    if (this.memory == null || !Enabled || !memory.ProcessHooked) return;

                    if (!freezeMapEnabled)
                    {
                        storedMap = n;
                        nowMapLabels.Text = LevelSelectorControl.Levels.Keys.FirstOrDefault(k => LevelSelectorControl.Levels[k] == n);
                    }
                });
            };
            //memory.Restart.OnValueChange += OnRestartChange;

            InitializeComponent();
            InitializeSecretLevel();
            // wire freeze level checkbox handler
            freezeLevelCheckbox.CheckedChanged += freezeLevelCheckbox_CheckedChanged;
            damageMaskformCheckbox.Enabled = false;
        }

        public DataControl()
        {
            InitializeComponent();
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
                // freeze to current map value
                var mapVal = memory.LoadMap.Read();
                var mapKey = LevelSelectorControl.Levels.Keys.FirstOrDefault(k => LevelSelectorControl.Levels[k] == mapVal);
                if (!string.IsNullOrEmpty(mapVal)) SetMapLock(mapVal, mapKey, true);
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

        private void OnMasksChange(int oldMasks, int newMasks)
        {
            SafeAction(() => {
                if (!Enabled || !memory.ProcessHooked) return;
                System.Diagnostics.Trace.WriteLine($"[DataControl] OnMasksChange: old={oldMasks}, new={newMasks}, stored={StoredMasks}, runtime={maskFreezeState.RuntimeMasks}, maskform={maskFreezeState.IsMaskFormOnDamage}");
                if (freezeMasksCheckbox.Checked)
                {
                    FreezeMasks();
                }
                else
                {
                    RefreshMasks();
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

        private void freezeMasksCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (freezeMasksCheckbox.Checked)
            {
                SyncStoredMasksWithCurrent();
                FreezeMasks();
                StartMasksFreeze();
                SafeAction(() => { damageMaskformCheckbox.Enabled = true; });
            }
            else
            {
                StopMasksFreeze();
                masksLabel.ForeColor = Color.Black;
                SafeAction(() =>
                {
                    damageMaskformCheckbox.Enabled = false;
                    try
                    {
                        suppressDamageMaskFormCheckboxEvent = true;
                        damageMaskformCheckbox.Checked = false;
                    }
                    finally { suppressDamageMaskFormCheckboxEvent = false; }
                });
                maskFreezeState.IsMaskFormOnDamage = false;
            }
        }

        private void SyncStoredMasksWithCurrent()
        {
            if (memory == null || !memory.ProcessHooked) return;

            StoredMasks = memory.Masks.Read();
        }

        private void damageMaskformCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressDamageMaskFormCheckboxEvent) return;

            maskFreezeState.IsMaskFormOnDamage = damageMaskformCheckbox.Checked;

            if (freezeMasksCheckbox.Checked)
            {
                FreezeMasks();
                StartMasksFreeze();
            }
        }

        private void masksUpButton_Click(object sender, EventArgs e)
        {
            int newMasks = memory.Masks.Read() + 1;

            StoredMasks = newMasks;

            memory.Masks.Write(StoredMasks);
            RefreshMasks(StoredMasks);
        }

        private void masksDownButton_Click(object sender, EventArgs e)
        {
            int newMasks = memory.Masks.Read() - 1;

            StoredMasks = newMasks;

            memory.Masks.Write(StoredMasks);
            RefreshMasks(StoredMasks);
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

        private void FreezeMasks()
        {
            if (memory == null || !Enabled || !memory.ProcessHooked) return;

            int currentMasks = memory.Masks.Read();
            int targetMasks = maskFreezeState.ResolveTargetMasks(currentMasks);

            if (currentMasks != targetMasks)
            {
                memory.Masks.Write(targetMasks);
                currentMasks = targetMasks;
            }

            maskFreezeState.SyncRuntimeMasks(currentMasks);
            UpdateMasksUi(currentMasks);
            masksLabel.ForeColor = Color.DodgerBlue;
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

        private void StartMasksFreeze()
        {
            StopMasksFreeze();
            masksFreezeTimer = new System.Threading.Timer(_ =>
            {
                FreezeMasks();
            }, null, 0, 10);
        }

        private void StopMasksFreeze()
        {
            masksFreezeTimer?.Dispose();
            masksFreezeTimer = null;
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

        private void RefreshMasks(int newMasks = -1)
        {
            int masks = newMasks != -1 ? newMasks : memory.Masks.Read();

            maskFreezeState.SyncRuntimeMasks(masks);

            UpdateMasksUi(masks);

            // only write when an explicit newMasks value was provided
            if (newMasks != -1)
            {
                memory.Masks.Write(newMasks);
            }
        }

        private void UpdateMasksUi(int masks)
        {
            SafeAction(() =>
            {
                masksLabel.Text = "Masks: " + masks;
                masksDownButton.Enabled = masks > 0;
                masksUpButton.Enabled = masks < 2;
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

        private void dataBox_EnabledChanged(object sender, EventArgs e)
        {
            if (memory == null) return;

            if (Enabled)
            {
                if (!freezeMasksCheckbox.Checked)
                {
                    SyncStoredMasksWithCurrent();
                }

                RefreshLives();
                RefreshMasks();

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
                    masksLabel.ForeColor = Color.DodgerBlue;
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
            StopLivesFreeze();
            StopMasksFreeze();
            _ = ShutdownLevelLockAsync();
            StopRestart();
            secretLevelTimer?.Dispose();
        }
    }
}
