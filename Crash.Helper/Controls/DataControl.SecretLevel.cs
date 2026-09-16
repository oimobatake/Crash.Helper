using System;
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

        internal static int GetSecretLevelValue(string level, bool enabled)
        {
            switch (level)
            {
                case "Crash 2 - Air Crash": return enabled ? 1 : 0;
                case "Crash 2 - Snow Go": return enabled ? 2 : 0;
                case "Crash 2 - Road to Ruin": return enabled ? 3 : 0;
                case "Crash 2 - Totally Bear": return 4;
                case "Crash 2 - Totally Fly": return 5;
                case "Crash 3 - Ski Crazed": return 6;
                case "Crash 3 - Hang'em High": return enabled ? 7 : 0;
                case "Crash 3 - Area 51?": return 8;
                case "Crash 3 - Future Frenzy": return enabled ? 9 : 0;
                case "Crash 3 - Rings of Power": return 10;
                case "Crash 3 - Hot Coco": return 11;
                case "Crash 3 - Eggipus Rex": return 12;
                case "Crash 3 - Future Tense": return 13;
                default: return 0;
            }
        }

        private void UpdateSecretLevel()
        {
            if (!Enabled || memory == null || !memory.ProcessHooked) return;
            var map = memory.LoadMap.Read();
            var level = LevelSelectorControl.Levels.FirstOrDefault(pair => pair.Value == map).Key;
            memory.SecretLevel.Write(GetSecretLevelValue(level, secretLevelCheckbox.Checked));
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
            StopMapFreeze();
            StopRestart();
            secretLevelTimer?.Dispose();
        }
    }
}
