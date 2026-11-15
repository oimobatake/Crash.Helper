using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Memory.Masks;

namespace Crash.Helper.Controls
{
    public partial class DataControl
    {
        private System.Threading.Timer masksFreezeTimer;
        private readonly MaskService maskEvents = new MaskService();
        private readonly System.Windows.Forms.Timer maskStatusTimer = new System.Windows.Forms.Timer { Interval = 16 };
        private bool legacyMaskOption, maskModeChanging;
        private bool UseLegacyMasks => legacyMaskOption || memory?.Profile?.Name != "Steam";

        internal void InitializeMaskOptions(bool legacy)
        {
            legacyMaskOption = legacy;
            maskStatusTimer.Tick += (s, e) => { if (!UseLegacyMasks && Enabled && !updateSuspended) RefreshEventMaskUi(); };
        }

        internal void ApplyInitialMaskOption(bool legacy) { legacyMaskOption = legacy; }

        private void ConfigureMaskEvents()
        {
            bool active = !UseLegacyMasks && Enabled && !updateSuspended && !maskModeChanging && memory.ProcessHooked;
            maskEvents.Configure(active ? memory.Masks.Process : null, freezeMasksCheckbox.Checked,
                StoredMasks, damageMaskformCheckbox.Checked);
            if (active) maskStatusTimer.Start(); else maskStatusTimer.Stop();
        }

        internal async Task SetLegacyMaskOptionAsync(bool legacy)
        {
            if (legacyMaskOption == legacy) return;
            maskModeChanging = true;
            masksFreezeTimer?.Dispose(); masksFreezeTimer = null; maskStatusTimer.Stop();
            try
            {
                await maskEvents.StopAsync();
                legacyMaskOption = legacy;
                maskFreezeState.ResetDeathSuspension();
                if (UseLegacyMasks) maskFreezeState.SetStoredMasks(StoredMasks);
                if (!UseLegacyMasks && damageMaskformCheckbox.Checked) maskFreezeState.SetStoredMasks(2);
            }
            finally { maskModeChanging = false; }
            ConfigureMaskEvents();
            if (freezeMasksCheckbox.Checked) { FreezeMasks(); StartMasksFreeze(); }
            RefreshMasks();
        }

        internal Task ShutdownMasksAsync() { maskStatusTimer.Stop(); return maskEvents.StopAsync(); }

        private void RefreshEventMaskUi()
        {
            var state = maskEvents.Latest;
            UpdateMasksUi(state.Binding != null ? maskEvents.DisplayCount : memory.Masks.Read());
        }
        private sealed class MaskFreezeState
        {
            public int StoredMasks { get; private set; } = 0;
            public int RuntimeMasks { get; private set; } = 0;
            public bool IsMaskFormOnDamage { get; set; } = false;
            private int? masksBeforeDeath;
            public bool DeathSuspended => masksBeforeDeath.HasValue;

            public void ResetDeathSuspension() => masksBeforeDeath = null;

            public void SetStoredMasks(int value)
            {
                if (value < 0) value = 0;
                if (value > 2) value = 2;
                StoredMasks = value;
            }

            public void SetEventTarget(int value) { StoredMasks = Math.Max(0, Math.Min(3, value)); }

            public int? ResolveTargetMasks(int currentMasks, bool isDead)
            {
                if (isDead)
                {
                    if (DeathSuspended) return null;
                    masksBeforeDeath = StoredMasks;
                    return 0;
                }
                if (DeathSuspended)
                {
                    StoredMasks = masksBeforeDeath.Value;
                    ResetDeathSuspension();
                    return StoredMasks;
                }
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
            set
            {
                if (MaskInputBlocked) return;
                SetMaskTarget(value);
                if (!UseLegacyMasks) { maskEvents.SetManualTarget(StoredMasks); ConfigureMaskEvents(); }
            }
        }

        private void SetMaskTarget(int value)
        {
            if (UseLegacyMasks) maskFreezeState.SetStoredMasks(value);
            else maskFreezeState.SetEventTarget(value);
        }

        private void OnMasksChange(int oldMasks, int newMasks)
        {
            if (!UseLegacyMasks) { SafeAction(RefreshEventMaskUi); return; }
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
                maskFreezeState.ResetDeathSuspension();
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
                ConfigureMaskEvents();
                if (memory != null) RefreshMasks();
            }
        }

        private void SyncStoredMasksWithCurrent()
        {
            if (memory == null || !memory.ProcessHooked) return;

            var state = maskEvents.Latest;
            SetMaskTarget(!UseLegacyMasks && state.Paused && state.CanConfigure ? maskEvents.DisplayCount : memory.Masks.Read());
        }

        private void damageMaskformCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressDamageMaskFormCheckboxEvent) return;

            maskFreezeState.IsMaskFormOnDamage = damageMaskformCheckbox.Checked;
            if (!UseLegacyMasks && damageMaskformCheckbox.Checked) maskFreezeState.SetStoredMasks(2);

            if (freezeMasksCheckbox.Checked)
            {
                FreezeMasks();
                StartMasksFreeze();
            }
        }

        private void masksUpButton_Click(object sender, EventArgs e)
        {
            ChangeMasks(1);
        }

        private void masksDownButton_Click(object sender, EventArgs e)
        {
            ChangeMasks(-1);
        }

        private bool MaskInputBlocked => maskModeChanging || (UseLegacyMasks
            ? freezeMasksCheckbox != null && freezeMasksCheckbox.Checked && (maskFreezeState.DeathSuspended || (memory != null && memory.IsDead))
            : !maskEvents.Latest.CanConfigure);

        public void ChangeMasks(int amount)
        {
            if (memory == null || !Enabled || updateSuspended || !memory.ProcessHooked || MaskInputBlocked) return;
            if (!UseLegacyMasks)
            {
                if (freezeMasksCheckbox.Checked && !damageMaskformCheckbox.Checked)
                {
                    SetMaskTarget(StoredMasks + amount);
                    maskEvents.SetManualTarget(StoredMasks);
                    ConfigureMaskEvents();
                }
                else maskEvents.Change(amount);
                return;
            }
            StoredMasks = memory.Masks.Read() + amount;
            RefreshMasks(StoredMasks);
        }


        private void FreezeMasks()
        {
            if (memory == null || !Enabled || updateSuspended || !memory.ProcessHooked || !freezeMasksCheckbox.Checked) return;
            if (!UseLegacyMasks) { ConfigureMaskEvents(); RefreshEventMaskUi(); return; }

            int currentMasks = memory.Masks.Read();
            int? targetMasks = maskFreezeState.ResolveTargetMasks(currentMasks, memory.IsDead);

            if (targetMasks.HasValue && currentMasks != targetMasks.Value)
            {
                memory.Masks.Write(targetMasks.Value);
                currentMasks = targetMasks.Value;
            }

            maskFreezeState.SyncRuntimeMasks(currentMasks);
            UpdateMasksUi(currentMasks);
        }


        private void StartMasksFreeze()
        {
            StopMasksFreeze();
            if (!UseLegacyMasks) { ConfigureMaskEvents(); return; }
            masksFreezeTimer = new System.Threading.Timer(_ =>
            {
                // Serialize death transitions and button/hotkey edits on the UI thread.
                SafeAction(FreezeMasks);
            }, null, 0, 10);
        }

        private void StopMasksFreeze()
        {
            masksFreezeTimer?.Dispose();
            masksFreezeTimer = null;
            if (memory != null && freezeMasksCheckbox != null) ConfigureMaskEvents();
        }


        private void RefreshMasks(int newMasks = -1)
        {
            if (!UseLegacyMasks)
            {
                if (newMasks >= 0 && !MaskInputBlocked) maskEvents.Change(newMasks - maskEvents.DisplayCount);
                RefreshEventMaskUi(); return;
            }
            if (newMasks != -1 && MaskInputBlocked) return;
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
                masksLabel.ForeColor = freezeMasksCheckbox.Checked
                    ? ((UseLegacyMasks ? maskFreezeState.DeathSuspended : maskEvents.Latest.Dead) ? SystemColors.GrayText : Color.DodgerBlue)
                    : Color.Black;
                masksDownButton.Enabled = !MaskInputBlocked && masks > 0;
                masksUpButton.Enabled = !MaskInputBlocked && (UseLegacyMasks ? masks < 2 :
                    freezeMasksCheckbox.Checked && !damageMaskformCheckbox.Checked ? StoredMasks < 3 :
                    (maskEvents.Latest.Paused || !maskEvents.Latest.Invincible) && masks < 3);
            });
        }

    }
}
