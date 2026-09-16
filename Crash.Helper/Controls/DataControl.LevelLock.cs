using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Controls
{
    public partial class DataControl
    {
        private readonly LevelLockService levelLock = new LevelLockService();
        private int levelLockRevision;
        private Task levelLockShutdown;
        private bool showCurrentLevel;

        internal void AttachLevelControls(System.Windows.Forms.GroupBox levelBox)
        {
            levelLabel.Text = "Level:";
            nowMapLabels.Text = "-";
            levelLabel.Location = new Point(7, 22);
            nowMapLabels.Location = new Point(46, 22);
            freezeLevelCheckbox.Location = new Point(7, 107);
            secretLevelCheckbox.Location = new Point(155, 107);
            var controls = new System.Windows.Forms.Control[] { levelLabel, nowMapLabels, freezeLevelCheckbox, secretLevelCheckbox };
            foreach (var control in controls)
            {
                control.Enabled = Enabled;
                levelBox.Controls.Add(control);
            }
            // These controls now live outside Data, but still follow the helper/game availability.
            EnabledChanged += (s, e) => { foreach (var control in controls) control.Enabled = Enabled; };
            Height = 100;
        }

        private static string GetMapCommand(string level)
        {
            if (string.IsNullOrWhiteSpace(level)) return string.Empty;
            level = level.Trim();
            return level.StartsWith("loadmap ", StringComparison.Ordinal) ? level : "loadmap " + level;
        }

        private static string GetLevelDisplayName(string level)
        {
            string command = GetMapCommand(level);
            return LevelSelectorControl.Levels.FirstOrDefault(pair => pair.Value == command).Key ?? string.Empty;
        }

        private void OnLoadMapChanged(string oldValue, string newValue)
        {
            SafeAction(() =>
            {
                if (memory == null || !Enabled || !memory.ProcessHooked) return;
                if (!freezeMapEnabled)
                {
                    storedMap = newValue;
                    // A new game-side command replaces the current-level display used after unlocking.
                    showCurrentLevel = false;
                }
                RefreshLevelDisplay();
            });
        }

        private void RefreshLevelDisplay()
        {
            if (memory == null || !Enabled || !memory.ProcessHooked) return;
            string map = showCurrentLevel ? null : memory.LoadMap.Read();
            if (string.IsNullOrWhiteSpace(map)) map = memory.CurrentLevel.Read();
            else if (freezeMapEnabled) map = storedMap;
            nowMapLabels.Text = GetLevelDisplayName(map);
        }

        public async void SetMapLock(string mapValue, string mapKey, bool startFreeze = true)
        {
            if (levelLockShutdown != null || !Enabled || memory == null || !memory.ProcessHooked || string.IsNullOrEmpty(mapValue)) return;
            mapValue = GetMapCommand(mapValue);
            if (mapValue.Length == 0) return;
            int revision = ++levelLockRevision;
            showCurrentLevel = false;
            storedMap = mapValue;
            freezeMapEnabled = startFreeze;
            ShowMapLock(mapKey, startFreeze, startFreeze ? Color.DarkGoldenrod : Color.Black);
            MapLockChanged?.Invoke(this, storedMap);
            var process = memory.LoadMap.Process;
            try
            {
                await levelLock.SetAsync(startFreeze ? process : null, startFreeze ? mapValue : null);
                if (revision != levelLockRevision || IsDisposed || levelLockShutdown != null || !Enabled || !memory.ProcessHooked) return;
                // Apply the selected map once; subsequent writes are intercepted inside the game.
                memory.LoadMap.Write(mapValue);
                ShowMapLock(mapKey, startFreeze, startFreeze ? Color.DodgerBlue : Color.Black);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportLevelLockFailure(revision, ex); }
        }

        public async void StopMapLock()
        {
            int revision = ++levelLockRevision;
            freezeMapEnabled = false;
            storedMap = null;
            showCurrentLevel = true;
            ShowMapLock(GetLevelDisplayName(memory?.CurrentLevel.Read()), false, Color.Black);
            MapLockChanged?.Invoke(this, null);
            if (levelLockShutdown != null) return;
            try { await levelLock.SetAsync(null, null); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportLevelLockFailure(revision, ex); }
        }

        private async void StopMapFreeze()
        {
            if (levelLockShutdown != null) return;
            int revision = ++levelLockRevision;
            try { await levelLock.SetAsync(null, null); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportLevelLockFailure(revision, ex); }
        }

        private void ShowMapLock(string label, bool enabled, Color color)
        {
            nowMapLabels.Text = label;
            nowMapLabels.ForeColor = color;
            suppressFreezeCheckboxEvent = true;
            try { freezeLevelCheckbox.Checked = enabled; }
            finally { suppressFreezeCheckboxEvent = false; }
        }

        private void ReportLevelLockFailure(int revision, Exception error)
        {
            System.Diagnostics.Trace.WriteLine("[LevelLock] " + error);
            if (revision != levelLockRevision || IsDisposed || levelLockShutdown != null) return;
            freezeMapEnabled = false;
            storedMap = null;
            showCurrentLevel = true;
            ShowMapLock(GetLevelDisplayName(memory?.CurrentLevel.Read()), false, Color.Black);
            MapLockChanged?.Invoke(this, null);
            System.Windows.Forms.MessageBox.Show(this, "Could not change level lock.\n" + error.Message,
                "Level lock", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }

        internal Task ShutdownLevelLockAsync()
        {
            if (levelLockShutdown == null || levelLockShutdown.IsFaulted || levelLockShutdown.IsCanceled)
            {
                ++levelLockRevision;
                levelLockShutdown = levelLock.ShutdownAsync();
            }
            return levelLockShutdown;
        }
    }
}
