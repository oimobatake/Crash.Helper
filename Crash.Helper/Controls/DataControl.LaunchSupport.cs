using System;
using System.Drawing;
using System.Windows.Forms;

namespace Crash.Helper.Controls
{
    public partial class DataControl
    {
        private Button _btnLaunchFromSelectedLevel;

        public event EventHandler LaunchFromSelectedLevelClicked;

        public void InitializeLaunchSupport()
        {
            if (_btnLaunchFromSelectedLevel != null)
            {
                return;
            }

            _btnLaunchFromSelectedLevel = new Button();
            _btnLaunchFromSelectedLevel.Name = "btnLaunchFromSelectedLevel";
            _btnLaunchFromSelectedLevel.Text = "選択中レベルでゲーム起動";
            _btnLaunchFromSelectedLevel.AutoSize = false;
            _btnLaunchFromSelectedLevel.Size = new Size(220, 24);
            _btnLaunchFromSelectedLevel.TabIndex = 999;
            _btnLaunchFromSelectedLevel.UseVisualStyleBackColor = true;
            _btnLaunchFromSelectedLevel.Click += btnLaunchFromSelectedLevel_Click;

            if (cmbLevelSelector != null)
            {
                _btnLaunchFromSelectedLevel.Location = new Point(
                    cmbLevelSelector.Left,
                    cmbLevelSelector.Bottom + 8);
            }
            else
            {
                _btnLaunchFromSelectedLevel.Location = new Point(8, 8);
            }

            Controls.Add(_btnLaunchFromSelectedLevel);
            _btnLaunchFromSelectedLevel.BringToFront();
        }

        public string GetSelectedLevelPath()
        {
            if (cmbLevelSelector == null)
            {
                return string.Empty;
            }

            var selectedValue = cmbLevelSelector.SelectedValue as string;
            if (!string.IsNullOrWhiteSpace(selectedValue))
            {
                return selectedValue;
            }

            var selectedItem = cmbLevelSelector.SelectedItem;
            if (selectedItem != null)
            {
                return selectedItem.ToString();
            }

            return cmbLevelSelector.Text ?? string.Empty;
        }

        public void ApplyHookStateForLaunch(bool isHooked)
        {
            if (cmbLevelSelector != null)
            {
                // Hookされていなくても操作可能
                cmbLevelSelector.Enabled = true;
            }

            if (_btnLaunchFromSelectedLevel != null)
            {
                // Hookされていない時だけ押せる
                _btnLaunchFromSelectedLevel.Enabled = !isHooked;
            }
        }

        private void btnLaunchFromSelectedLevel_Click(object sender, EventArgs e)
        {
            if (LaunchFromSelectedLevelClicked != null)
            {
                LaunchFromSelectedLevelClicked(this, EventArgs.Empty);
            }
        }
    }
}