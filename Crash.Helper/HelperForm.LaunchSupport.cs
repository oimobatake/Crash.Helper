using System;
using System.Windows.Forms;
using Crash.Helper.Launcher;

namespace Crash.Helper
{
    public partial class HelperForm
    {
        private bool _isHookedForLaunchFeature;

        private void InitializeLaunchSupport()
        {
            dataControl1.InitializeLaunchSupport();
            dataControl1.LaunchFromSelectedLevelClicked += dataControl1_LaunchFromSelectedLevelClicked;
            dataControl1.ApplyHookStateForLaunch(_isHookedForLaunchFeature);
        }

        private void SetHookStateForLaunchFeature(bool isHooked)
        {
            _isHookedForLaunchFeature = isHooked;
            dataControl1.ApplyHookStateForLaunch(isHooked);
        }

        private void dataControl1_LaunchFromSelectedLevelClicked(object sender, EventArgs e)
        {
            if (_isHookedForLaunchFeature)
            {
                return;
            }

            var levelPath = dataControl1.GetSelectedLevelPath();
            var launched = SteamGameLauncher.Launch(levelPath);

            if (!launched)
            {
                MessageBox.Show(
                    "Steam が既定パスに見つかりません。\r\nC:\\Program Files (x86)\\Steam\\steam.exe",
                    "起動失敗",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}