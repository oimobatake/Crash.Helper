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
        private int storedMasks = 0;
        public int StoredMasks
        {
            get{ return storedMasks; }
            set
            {
                if (value < 0) value = 0;
                if (value > 2) value = 2;
                storedMasks = value;
            }
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
            this.memory = memory;

            memory.Lives.OnValueChange += OnLivesChange;
            memory.Masks.OnValueChange += OnMasksChange;
            memory.LoadMap.OnValueChange += (o, n) => {
                SafeAction(() =>
                {
                    try
                    {
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
                                SetMapLock(n, false);
                                System.Diagnostics.Trace.WriteLine($"[DataControl] OnValueChange: freeze disabled, updated storedMap via SetMapLock new='{n}' (old='{o}')");
                            }
                        }
                    }
                    catch { }

                    //oldMapLabels.Text = o;
                    nowMapLabels.Text = n;
                });
            };
            memory.Restart.OnValueChange += OnRestartChange;

            InitializeComponent();
            // wire freeze level checkbox handler
            try { freezeLevelCheckbox.CheckedChanged += freezeLevelCheckbox_CheckedChanged; } catch { }
        }

        public DataControl()
        {
            InitializeComponent();
            try { freezeLevelCheckbox.CheckedChanged += freezeLevelCheckbox_CheckedChanged; } catch { }
        }

        private void freezeLevelCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressFreezeCheckboxEvent) return;

            try
            {
                if (freezeLevelCheckbox.Checked)
                {
                    // freeze to current map value
                    try
                    {
                        var mapVal = memory.LoadMap.Read();
                        if (!string.IsNullOrEmpty(mapVal)) SetMapLock(mapVal, true);
                    }
                    catch { }
                }
                else
                {
                    StopMapLock();
                }
            }
            catch { }
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
                FreezeMasks();
                StartMasksFreeze();
            }
            else
            {
                StopMasksFreeze();
                masksLabel.ForeColor = Color.Black;
            }
        }

        private void masksUpButton_Click(object sender, EventArgs e)
        {
            int newMasks = memory.Masks.Read() + 1;

            StoredMasks = newMasks;

            memory.Masks.Write(storedMasks);
            RefreshMasks(storedMasks);
        }

        private void masksDownButton_Click(object sender, EventArgs e)
        {
            int newMasks = memory.Masks.Read() - 1;

            StoredMasks = newMasks;

            memory.Masks.Write(storedMasks);
            RefreshMasks(storedMasks);
        }

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
            memory.Masks.Write(storedMasks);
            RefreshMasks(storedMasks);
            masksLabel.ForeColor = Color.DodgerBlue;
        }

        private void DisplayRestart(bool restart)
        {
            memory.Restart.Write(restart ? (byte)1 : (byte)0);
        }

        private void FreezeMap()
        {
            // mark internal flag
            freezeMapEnabled = true;
            // store current map value and write it immediately so it becomes the enforced value
            storedMap = memory.LoadMap.Read();
            System.Diagnostics.Trace.WriteLine($"[DataControl] FreezeMap: storing map='{storedMap}'");
            try
            {
                if (!string.IsNullOrEmpty(storedMap))
                {
                    memory.LoadMap.Write(storedMap);
                }
            }
            catch { }

            SafeAction(() =>
            {
                nowMapLabels.ForeColor = Color.DodgerBlue;
            });
        }

        private void StartLivesFreeze()
        {
            StopLivesFreeze();
            livesFreezeTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (storedLives >= 0) memory.Lives.Write(storedLives);
                } catch { }
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
                try
                {
                    memory.Masks.Write(storedMasks);
                } catch { }
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
                        SafeAction(() =>
                        {
                            nowMapLabels.ForeColor = Color.Black;
                        });
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

            SafeAction(() =>
            {
                masksLabel.Text = "Masks: " + masks;
                masksDownButton.Enabled = masks > 0;
                masksUpButton.Enabled = masks < 2;
            });

            // only write when an explicit newMasks value was provided
            if (newMasks != -1)
            {
                memory.Masks.Write(newMasks);
            }
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
                catch { }
            }
            else
            {
                action();
            }
        }

        private void dataBox_EnabledChanged(object sender, EventArgs e)
        {
            if (Enabled)
            {
                RefreshLives();
                RefreshMasks();

                if (freezeLivesCheckbox.Checked)
                {
                    // 再フック時は直前の保持値を優先して再適用する
                    if (storedLives < 0)
                    {
                        storedLives = memory.Lives.Read();
                    }
                    try { memory.Lives.Write(storedLives); } catch { }
                    livesLabel.ForeColor = Color.DodgerBlue;
                    StartLivesFreeze();
                }
                if (freezeMasksCheckbox.Checked)
                {
                    // 再フック時は直前の保持値を再適用する
                    try { memory.Masks.Write(storedMasks); } catch { }
                    RefreshMasks(storedMasks);
                    masksLabel.ForeColor = Color.DodgerBlue;
                    StartMasksFreeze();
                }
                if (freezeMapEnabled)
                {
                    // 再フック時は直前のstoredMapを優先して再適用する
                    if (!string.IsNullOrEmpty(storedMap))
                    {
                        SetMapLock(storedMap, true);
                    }
                    else
                    {
                        // 保持値がない場合のみ現在Mapを保持してfreeze開始
                        FreezeMap();
                        StartMapFreeze();
                    }
                }

                if (restartForceEnabled || displayRestartCheckBox.Checked)
                {
                    // 再フック時にRestart強制状態を復元
                    restartForceEnabled = true;
                    try { memory.Restart.Write((byte)1); } catch { }
                    StartRestart();
                }
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
        public void SetMapLock(string mapValue, bool startFreeze = true)
        {
            if (string.IsNullOrEmpty(mapValue)) return;

            try
            {
                System.Diagnostics.Trace.WriteLine($"[DataControl] SetMapLock: map='{mapValue}' startFreeze={startFreeze}");
                // write the value to memory first
                memory.LoadMap.Write(mapValue);
                // store and update UI
                storedMap = mapValue;
                SafeAction(() =>
                {
                    nowMapLabels.Text = "nowMap: " + mapValue;
                    if (startFreeze) nowMapLabels.ForeColor = Color.DodgerBlue;
                    else nowMapLabels.ForeColor = Color.Black;
                        // internal flag controls freeze behavior
                        freezeMapEnabled = startFreeze;
                    // reflect in UI checkbox without triggering handler
                    try
                    {
                        suppressFreezeCheckboxEvent = true;
                        freezeLevelCheckbox.Checked = startFreeze;
                    }
                    finally { suppressFreezeCheckboxEvent = false; }
                });

                if (startFreeze)
                {
                    StartMapFreeze();
                }

                // notify listeners
                try { MapLockChanged?.Invoke(this, storedMap); } catch { }
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
                SafeAction(() =>
                {
                    // keep UI checkbox hidden/unused
                    nowMapLabels.ForeColor = Color.Black;
                    try
                    {
                        suppressFreezeCheckboxEvent = true;
                        freezeLevelCheckbox.Checked = false;
                    }
                    finally { suppressFreezeCheckboxEvent = false; }
                });
                System.Diagnostics.Trace.WriteLine("[DataControl] StopMapLock: stopped and cleared storedMap");
                try { MapLockChanged?.Invoke(this, null); } catch { }
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[DataControl] StopMapLock: exception: {ex}"); }
        }
    }
}
