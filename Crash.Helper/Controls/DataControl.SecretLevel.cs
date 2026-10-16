using System;
using Crash.Helper.Memory;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Crash.Helper.Controls
{
    public partial class DataControl
    {
        private CheckBox secretLevelCheckbox;
        private System.Windows.Forms.Timer secretLevelTimer;
        private bool secretBeforeLaunch;
        private string launchSelection, pendingSecretLevel;
        private bool pendingSecretChecked, launchLoadingSeen;
        private int launchSecretProcessId;
        private DateTime launchSecretDeadline, launchReadySince;
        private bool refreshingSecretCheckbox;

        internal void SetLaunchSecretLevel(string level)
        {
            launchSelection = level;
            UpdateSecretLevel();
        }

        internal void SetSecretLaunchAvailability(bool enabled)
        {
            secretBeforeLaunch = enabled;
            UpdateSecretLevel();
        }

        internal void BeginLaunchSecret(string map)
        {
            if (memory == null || memory.ProcessHooked) return;
            pendingSecretLevel = GetLevelDisplayName(map);
            if (string.IsNullOrEmpty(pendingSecretLevel)) { pendingSecretLevel = null; return; }
            pendingSecretChecked = secretLevelCheckbox.Checked;
            launchSecretProcessId = 0;
            launchLoadingSeen = false;
            launchReadySince = DateTime.MinValue;
            launchSecretDeadline = DateTime.UtcNow.AddSeconds(60);
            UpdateSecretLevel();
        }

        internal void CancelLaunchSecret()
        {
            pendingSecretLevel = null;
            launchSecretProcessId = 0;
            UpdateSecretLevel();
        }

        private void InitializeSecretLevel()
        {
            secretLevelCheckbox = new CheckBox { Text = "Secret level", AutoSize = true, Location = new Point(125, 137), TabIndex = 25, Enabled = false };
            dataBox.Controls.Add(secretLevelCheckbox);
            secretLevelTimer = new System.Windows.Forms.Timer { Interval = 10 };
            secretLevelTimer.Tick += (s, e) => UpdateSecretLevel();
            secretLevelCheckbox.CheckedChanged += (s, e) => { if (!refreshingSecretCheckbox) UpdateSecretLevel(); };
            EnabledChanged += (s, e) => UpdateSecretLevel();
        }

        private void UpdateSecretLevel()
        {
            if (secretLevelCheckbox == null || secretLevelTimer == null) return;
            if (updateSuspended) { secretLevelCheckbox.Enabled = false; secretLevelTimer.Stop(); return; }
            if (pendingSecretLevel != null)
            {
                if (DateTime.UtcNow >= launchSecretDeadline ||
                    (launchSecretProcessId != 0 && (!memory.ProcessHooked || memory.LoadMap.Process?.Id != launchSecretProcessId)))
                    pendingSecretLevel = null;
                else if (memory.ProcessHooked && memory.IsSupportedVersion && Enabled)
                {
                    launchSecretProcessId = memory.LoadMap.Process.Id;
                    bool loading = memory.IsLoading;
                    launchLoadingSeen |= loading;
                    memory.SecretLevel.Write(SecretLevelRules.GetValue(pendingSecretLevel, pendingSecretChecked));
                    if (!loading && GetLevelDisplayName(memory.CurrentLevel.Read()) == pendingSecretLevel)
                    {
                        if (launchReadySince == DateTime.MinValue) launchReadySince = DateTime.UtcNow;
                        if (launchLoadingSeen || (DateTime.UtcNow - launchReadySince).TotalMilliseconds >= 100)
                            pendingSecretLevel = null;
                    }
                    else launchReadySince = DateTime.MinValue;
                }
            }
            bool active = !updateSuspended && Enabled && memory != null && memory.ProcessHooked && memory.IsSupportedVersion && freezeMapEnabled && mapLockActive;
            var level = active ? GetLevelDisplayName(storedMap) : null;
            bool preparing = secretBeforeLaunch && memory != null && !memory.ProcessHooked && pendingSecretLevel == null;
            string editableLevel = preparing ? launchSelection : level;
            secretLevelCheckbox.Enabled = (active || preparing) && SecretLevelRules.GetValue(editableLevel, true) != SecretLevelRules.GetValue(editableLevel, false);
            if (pendingSecretLevel != null) secretLevelCheckbox.Enabled = false;
            bool canRead = memory != null && memory.ProcessHooked && memory.IsSupportedVersion;
            if (!secretLevelCheckbox.Enabled && canRead)
            {
                // Mirror game state without treating this display change as a user edit.
                refreshingSecretCheckbox = true;
                try { secretLevelCheckbox.Checked = memory.SecretLevel.Read() != 0; }
                finally { refreshingSecretCheckbox = false; }
            }
            secretLevelTimer.Interval = active || pendingSecretLevel != null ? 10 : 100;
            if (pendingSecretLevel != null) { secretLevelTimer.Start(); return; }
            if (!active) { if (canRead) secretLevelTimer.Start(); else secretLevelTimer.Stop(); return; }
            secretLevelTimer.Start();
            memory.SecretLevel.Write(SecretLevelRules.GetValue(level, secretLevelCheckbox.Checked));
        }

        public void ToggleSecretLevel() { if (secretLevelCheckbox.Enabled) secretLevelCheckbox.Checked = !secretLevelCheckbox.Checked; }
    }
}
