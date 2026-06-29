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
        // Event fired whenever LoadMap changes. Parameter is the current map value.
        public event EventHandler<string> CurrentMapChanged;

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
        private System.Threading.Timer mapFreezeTimer;
        private int mapFreezeFailCount = 0;
        private const int MapFreezeFailThreshold = 5;
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
                    if (this.memory == null) return;

                    if (freezeMapEnabled)
                    {
                        // if we have a storedMap enforced, reapply that; otherwise fall back to old value
                        if (!string.IsNullOrEmpty(storedMap))
                        {
                            memory.LoadMap.Write(storedMap);
                            System.Diagnostics.Trace.WriteLine($"[DataControl] OnValueChange: reapplying storedMap='{storedMap}' (old='{o}', new='{n}')");
                        }
                        else
                        {
                            memory.LoadMap.Write(o);
                            System.Diagnostics.Trace.WriteLine($"[DataControl] OnValueChange: freeze enabled, restored old='{o}' (new='{n}')");
                        }
                    }
                    else
                    {
                        // Freezeしていない場合は、変更されたMapをSetMapLock(startFreeze:false)経由で保持値へ反映する
                        if (!string.IsNullOrEmpty(n))
                        {
                            var mapKey = LevelSelectorControl.Levels.Keys.FirstOrDefault(k => LevelSelectorControl.Levels[k] == n);
                            SetMapLock(n, mapKey, false);
                            System.Diagnostics.Trace.WriteLine($"[DataControl] OnValueChange: freeze disabled, updated storedMap via SetMapLock new='{n}' (old='{o}')");
                        }
                    }

                    //oldMapLabels.Text = o;
                    //var dispValue = LevelSelectorControl.Levels.Keys.FirstOrDefault(k => LevelSelectorControl.Levels[k] == n);
                    //nowMapLabels.Text = dispValue;

                    CurrentMapChanged?.Invoke(this, n);
                });
            };
            //memory.Restart.OnValueChange += OnRestartChange;

            InitializeComponent();
            damageMaskformCheckbox.Enabled = false;
        }

        public DataControl()
        {
            InitializeComponent();
            damageMaskformCheckbox.Enabled = false;
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
            if (memory == null) return;

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

        private void FreezeMap()
        {
            if (memory == null) return;

            // mark internal flag
            freezeMapEnabled = true;
            // store current map value and write it immediately so it becomes the enforced value
            storedMap = memory.LoadMap.Read();
            System.Diagnostics.Trace.WriteLine($"[DataControl] FreezeMap: storing map='{storedMap}'");
            if (!string.IsNullOrEmpty(storedMap))
            {
                memory.LoadMap.Write(storedMap);
            }

            //SafeAction(() =>
            //{
            //    nowMapLabels.ForeColor = Color.DodgerBlue;
            //});
        }

        private void StartLivesFreeze()
        {
            StopLivesFreeze();
            livesFreezeTimer = new System.Threading.Timer(_ =>
            {
                if (memory == null) return;

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

        private void StartMapFreeze()
        {
            StopMapFreeze();
            // use a moderate interval and track failures. Timer interval set to 50ms.
            mapFreezeTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(storedMap))
                    {
                        memory.LoadMap.Write(storedMap);
                        // reset failure counter on success
                        mapFreezeFailCount = 0;
                    }
                }
                catch
                {
                    // increment failure count; if too many consecutive failures, stop freeze
                    mapFreezeFailCount++;
                    if (mapFreezeFailCount >= MapFreezeFailThreshold)
                    {
                        StopMapFreeze();
                        //SafeAction(() =>
                        //{
                        //    nowMapLabels.ForeColor = Color.Black;
                        //});
                    }
                }
            }, null, 0, 1);
        }

        private void StopMapFreeze()
        {
            mapFreezeTimer?.Dispose();
            mapFreezeTimer = null;
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
                        // When no stored value exists, capture current map and start freeze.
                        FreezeMap();
                        StartMapFreeze();
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

        // Public API to set or stop map lock from external UI
        public void SetMapLock(string mapValue, string mapKey, bool startFreeze = true, bool writeToMemory = true)
        {
            if (string.IsNullOrEmpty(mapValue)) return;

            try
            {
                System.Diagnostics.Trace.WriteLine($"[DataControl] SetMapLock: map='{mapValue}' startFreeze={startFreeze} writeToMemory={writeToMemory}");
                if (!startFreeze)
                {
                    StopMapFreeze();
                }

                if (writeToMemory)
                {
                    memory.LoadMap.Write(mapValue);
                }

                // store and update UI
                storedMap = mapValue;
                SafeAction(() =>
                {
                    //nowMapLabels.Text = mapKey;
                    //if (startFreeze) nowMapLabels.ForeColor = Color.DodgerBlue;
                    //else if (!writeToMemory) nowMapLabels.ForeColor = Color.DarkOrange;
                    //else nowMapLabels.ForeColor = Color.Black;
                    // internal flag controls freeze behavior
                    freezeMapEnabled = startFreeze;
                });

                if (startFreeze)
                {
                    StartMapFreeze();
                }

                // notify listeners
                MapLockChanged?.Invoke(this, storedMap);
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[DataControl] SetMapLock: exception: {ex}"); }
        }

        public void StopMapLock()
        {
            try
            {
                // disable internal flag and stop enforcing
                freezeMapEnabled = false;
                StopMapFreeze();
                storedMap = null;

                //SafeAction(() =>
                //{
                //    // keep UI label neutral
                //    nowMapLabels.ForeColor = Color.Black;
                //});
                System.Diagnostics.Trace.WriteLine("[DataControl] StopMapLock: stopped and cleared storedMap");
                MapLockChanged?.Invoke(this, null);
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[DataControl] StopMapLock: exception: {ex}"); }
        }
    }
}
