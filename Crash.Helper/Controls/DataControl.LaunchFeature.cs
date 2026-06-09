using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Crash.Helper.Launcher;

namespace Crash.Helper.Controls
{
    public partial class DataControl
    {
        private Button _launchButton;
        private ComboBox _levelSelectorComboBox;
        private Timer _hookStateTimer;
        private bool _launchFeatureInitialized;

        public void InitializeLaunchFeature()
        {
            if (_launchFeatureInitialized)
            {
                return;
            }

            _launchFeatureInitialized = true;

            _levelSelectorComboBox = FindLevelSelectorComboBox(this);
            _launchButton = new Button
            {
                Name = "btnLaunchFromSelectedLevel",
                Text = "選択中レベルでゲーム起動",
                AutoSize = false,
                Size = new Size(220, 24),
                UseVisualStyleBackColor = true
            };

            if (_levelSelectorComboBox != null)
            {
                _launchButton.Location = new Point(_levelSelectorComboBox.Left, _levelSelectorComboBox.Bottom + 8);
            }
            else
            {
                _launchButton.Location = new Point(8, 8);
            }

            _launchButton.Click += LaunchButton_Click;
            Controls.Add(_launchButton);
            _launchButton.BringToFront();

            _hookStateTimer = new Timer { Interval = 300 };
            _hookStateTimer.Tick += HookStateTimer_Tick;
            _hookStateTimer.Start();

            UpdateLaunchUi();
        }

        private void HookStateTimer_Tick(object sender, EventArgs e)
        {
            UpdateLaunchUi();
        }

        private void UpdateLaunchUi()
        {
            var hooked = IsMemoryHooked();

            if (_levelSelectorComboBox != null)
            {
                // Hookなしでも使えるように常時有効
                _levelSelectorComboBox.Enabled = true;
            }

            if (_launchButton != null)
            {
                // Hookされていない時だけ押せる
                _launchButton.Enabled = !hooked;
            }
        }

        private void LaunchButton_Click(object sender, EventArgs e)
        {
            if (IsMemoryHooked())
            {
                return;
            }

            var levelPath = GetSelectedLevelPath();
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

        private string GetSelectedLevelPath()
        {
            if (_levelSelectorComboBox == null)
            {
                return string.Empty;
            }

            var selectedValue = _levelSelectorComboBox.SelectedValue as string;
            if (!string.IsNullOrWhiteSpace(selectedValue))
            {
                return selectedValue;
            }

            var selectedItem = _levelSelectorComboBox.SelectedItem;
            if (selectedItem != null)
            {
                return selectedItem.ToString();
            }

            return _levelSelectorComboBox.Text ?? string.Empty;
        }

        private static ComboBox FindLevelSelectorComboBox(Control root)
        {
            var allControls = GetAllChildren(root);

            var byName = allControls
                .OfType<ComboBox>()
                .FirstOrDefault(c => c.Name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0
                                  || c.Name.IndexOf("selector", StringComparison.OrdinalIgnoreCase) >= 0);

            if (byName != null)
            {
                return byName;
            }

            return allControls.OfType<ComboBox>().FirstOrDefault();
        }

        private static Control[] GetAllChildren(Control root)
        {
            return root.Controls.Cast<Control>()
                .SelectMany(c => new[] { c }.Concat(GetAllChildren(c)))
                .ToArray();
        }

        private static bool IsMemoryHooked()
        {
            // 既存ProcessControlへのコンパイル依存を避ける（エラー回避）
            // 想定プロパティ: IsHooked / Hooked
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("Crash.Helper.Controls.ProcessControl");
                if (type == null)
                {
                    continue;
                }

                var prop = type.GetProperty("IsHooked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                           ?? type.GetProperty("Hooked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                if (prop != null && prop.PropertyType == typeof(bool))
                {
                    return (bool)prop.GetValue(null, null);
                }
            }

            return false;
        }
    }
}