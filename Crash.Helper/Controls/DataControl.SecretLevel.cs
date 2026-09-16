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
            secretLevelCheckbox = new CheckBox { Text = "Secret level", AutoSize = true, Location = new Point(125, 137), TabIndex = 25 };
            dataBox.Controls.Add(secretLevelCheckbox);
            secretLevelTimer = new System.Windows.Forms.Timer { Interval = 10 };
            secretLevelTimer.Tick += (s, e) => UpdateSecretLevel();
            secretLevelCheckbox.CheckedChanged += (s, e) => UpdateSecretLevel();
            EnabledChanged += (s, e) =>
            {
                if (Enabled && memory != null && memory.ProcessHooked) { UpdateSecretLevel(); secretLevelTimer.Start(); }
                else secretLevelTimer.Stop();
            };
        }

        private void UpdateSecretLevel()
        {
            if (!Enabled || memory == null || !memory.ProcessHooked) return;
            var map = memory.LoadMap.Read();
            var level = LevelSelectorControl.Levels.FirstOrDefault(pair => pair.Value == map).Key;
            memory.SecretLevel.Write(SecretLevelRules.GetValue(level, secretLevelCheckbox.Checked));
        }

        public void ToggleSecretLevel() { if (Enabled) secretLevelCheckbox.Checked = !secretLevelCheckbox.Checked; }
    }
}
