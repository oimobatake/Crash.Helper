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

        private void InitializeSecretLevel()
        {
            secretLevelCheckbox = new CheckBox { Text = "Secret level", AutoSize = true, Location = new Point(125, 137), TabIndex = 25, Enabled = false };
            dataBox.Controls.Add(secretLevelCheckbox);
            secretLevelTimer = new System.Windows.Forms.Timer { Interval = 10 };
            secretLevelTimer.Tick += (s, e) => UpdateSecretLevel();
            secretLevelCheckbox.CheckedChanged += (s, e) => UpdateSecretLevel();
            EnabledChanged += (s, e) => UpdateSecretLevel();
        }

        private void UpdateSecretLevel()
        {
            if (secretLevelCheckbox == null || secretLevelTimer == null) return;
            bool active = Enabled && memory != null && memory.ProcessHooked && memory.IsSupportedVersion && freezeMapEnabled && mapLockActive;
            var level = active ? GetLevelDisplayName(storedMap) : null;
            secretLevelCheckbox.Enabled = active && SecretLevelRules.GetValue(level, true) != SecretLevelRules.GetValue(level, false);
            if (!active) { secretLevelTimer.Stop(); return; }
            secretLevelTimer.Start();
            memory.SecretLevel.Write(SecretLevelRules.GetValue(level, secretLevelCheckbox.Checked));
        }

        public void ToggleSecretLevel() { if (secretLevelCheckbox.Enabled) secretLevelCheckbox.Checked = !secretLevelCheckbox.Checked; }
    }
}
