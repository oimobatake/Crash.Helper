using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Memory;
using Crash.Helper;
using Crash.Helper.Input;

namespace Crash.Helper.Controls
{
    public partial class HotkeyControl : UserControl
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private CrashMemory memory;
        private DataControl data;
        private Hotkey[] hotkeys;
        private Label[] labels;
        private Label[] hotkeyLabels;
        private TextBox[] textboxes;
        private IGamepadListener gamepadListener;
        private ComboDetector comboDetector;
        private int? pendingGamepadIndex = null;
        private ushort pendingAssignMask = 0;
        private System.Threading.Timer pendingAssignTimer;
        private readonly int assignWindowMs = 300;

        public HotkeyControl(CrashMemory memory, DataControl data)
        {
            this.memory = memory;
            this.data = data;

            InitializeComponent();

            hotkeys = new[]
            {
                new Hotkey("Set Max lives: ", KeyModifiers.Shift, (uint)Keys.O, () => { memory.Lives.Write(99); data.Lives = 99; }),
                new Hotkey("Give one mask (+): ", KeyModifiers.Shift, (uint)Keys.F, () => { int masks = memory.Masks.Read() + 1; if (masks > 2) return; memory.Masks.Write(masks); data.Masks = masks; }),
                new Hotkey("Give one mask (-): ", KeyModifiers.Shift, (uint)Keys.D, () => { int masks = memory.Masks.Read() - 1; if (masks < 0) return; memory.Masks.Write(masks); data.Masks = masks; }),
                new Hotkey("Freeze level: ", KeyModifiers.Shift, (uint)Keys.L, () => {
                    try
                    {
                        // toggle freeze: if already frozen, stop; otherwise freeze to current map
                        if (data.IsMapFrozen)
                        {
                            data.StopMapLock();
                        }
                        else
                        {
                            // read current map value from memory (internal path)
                            var mapVal = memory.LoadMap.Read();
                            if (!string.IsNullOrEmpty(mapVal)) data.SetMapLock(mapVal, true);
                        }
                    }
                    catch { }
                })
            };

            // Keep these ordered and in sync with designer controls
            labels = new[] { zeroLivesLabel, giveMaskLabel, label1 };
            hotkeyLabels = new[] { zeroLivesHotkeyLabel, addMaskHotkeyLabel, subMaskHotkeyLabel, freezeLevelHotkeyLabel };
            textboxes = new[] { zeroLivesHotkeyTextbox, addMaskHotkeyTextbox, subMaskHotkeyTextbox, freezeLevelHotkeyTextbox };

            // Update labels for each registered hotkey
            zeroLivesHotkeyLabel.Text = hotkeys[0].ToString();
            zeroLivesHotkeyLabel.ForeColor = Color.ForestGreen;
            addMaskHotkeyLabel.Text = hotkeys[1].ToString();
            addMaskHotkeyLabel.ForeColor = Color.ForestGreen;
            subMaskHotkeyLabel.Text = hotkeys[2].ToString();
            subMaskHotkeyLabel.ForeColor = Color.ForestGreen;
            freezeLevelHotkeyLabel.Text = hotkeys[3].ToString();
            freezeLevelHotkeyLabel.ForeColor = Color.ForestGreen;

            RegisterHotkeys();

            try
            {
                // prefer WinRT listener if available, fallback to XInput
                IGamepadListener win = null;
                try { win = new WinRTGamepadListener(); } catch { win = null; }
                if (win != null)
                {
                    gamepadListener = win;
                }
                else
                {
                    gamepadListener = new XInputListener();
                }

                comboDetector = new ComboDetector();
                gamepadListener.ButtonPressed += Gamepad_ButtonPressed;
                // listen for map lock changes so we can update UI selection when freeze is toggled
                try { data.MapLockChanged += Data_MapLockChanged; } catch { }
            }
            catch { /* ignore if no input backend available */ }
        }

        private void Data_MapLockChanged(object sender, string mapValue)
        {
            // when map lock changes, update the LevelSelector combo selection if freeze checkbox is set
            try
            {
                if (data == null) return;
                if (data.InvokeRequired)
                {
                    data.BeginInvoke((Action)(() => Data_MapLockChanged(sender, mapValue)));
                    return;
                }

                if (!data.IsMapFrozen) return;

                // find matching display name from LevelSelector map dictionary
                var ls = this.Parent?.Controls.OfType<LevelSelectorControl>().FirstOrDefault();
                if (ls == null) return;
                // reflect selection via public API on LevelSelectorControl (use reflection if necessary)
                var comboField = typeof(LevelSelectorControl).GetField("combo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (comboField == null) return;
                var combo = comboField.GetValue(ls) as ComboBox;
                if (combo == null) return;

                // mapValue is internal map path; find display name in Levels dictionary
                var levelsField = typeof(LevelSelectorControl).GetField("Levels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (levelsField == null) return;
                var levels = levelsField.GetValue(null) as System.Collections.IDictionary;
                if (levels == null) return;

                string foundDisplay = null;
                foreach (System.Collections.DictionaryEntry de in levels)
                {
                    if (de.Value as string == mapValue)
                    {
                        foundDisplay = de.Key as string;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(foundDisplay))
                {
                    for (int i = 0; i < combo.Items.Count; i++)
                    {
                        if (combo.Items[i] as string == foundDisplay)
                        {
                            combo.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        public void RegisterHotkeys()
        {
            int keyCount = 0;
            foreach (Hotkey hotkey in hotkeys)
            {
                RegisterHotKey(Handle, keyCount++, (uint)hotkey.Modifier, hotkey.Key);
            }
        }

        public void UnregisterHotkeys()
        {
            for (int i = 0; i < hotkeys.Length; i++)
            {
                UnregisterHotKey(Handle, i);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg != 0x0312) return;

            int id = m.WParam.ToInt32();
            if (id >= 0 && id < hotkeys.Length)
            {
                try { hotkeys[id].Callback(); } catch { }
            }
        }

        private void Gamepad_ButtonPressed(object sender, GamepadButtonEventArgs e)
        {
            // If user is in assignment mode, bind the pressed button
            if (pendingGamepadIndex.HasValue)
            {
                // accumulate pressed buttons into a mask until timer elapses
                pendingAssignMask |= (ushort)e.Button;
                // reset timer
                pendingAssignTimer?.Change(assignWindowMs, System.Threading.Timeout.Infinite);
                if (pendingAssignTimer == null)
                {
                    pendingAssignTimer = new System.Threading.Timer(_ => FinalizePendingAssignment(), null, assignWindowMs, System.Threading.Timeout.Infinite);
                }
                // reflect interim state in UI
                int idxPreview = pendingGamepadIndex.Value;
                if (IsHandleCreated)
                {
                    string preview = MaskToNames(pendingAssignMask);
                    if (InvokeRequired) BeginInvoke((Action)(() => hotkeyLabels[idxPreview].Text = hotkeys[idxPreview].ToString() + " [" + preview + "]"));
                    else hotkeyLabels[idxPreview].Text = hotkeys[idxPreview].ToString() + " [" + preview + "]";
                }
                return;
            }

            // Dispatch any matching gamepad hotkey
            if (!enabledCheckbox.Checked) return;
            foreach (var hk in hotkeys)
            {
                if (hk.GamepadMask.HasValue && (hk.GamepadMask.Value & (ushort)e.Button) != 0)
                {
                    try { hk.Callback(); } catch { }
                }
            }

            // update UI bindings display
            UpdateGamepadBindingsLabel();
        }

        private void FinalizePendingAssignment()
        {
            // called on threadpool
            int idx = -1;
            if (pendingGamepadIndex.HasValue) idx = pendingGamepadIndex.Value;
            if (idx >= 0 && idx < hotkeys.Length)
            {
                hotkeys[idx].GamepadMask = pendingAssignMask;
                pendingAssignMask = 0;
                pendingGamepadIndex = null;
                pendingAssignTimer?.Dispose();
                pendingAssignTimer = null;
                UpdateGamepadBindingsLabel();
            }
        }

        private static string MaskToNames(ushort mask)
        {
            var names = new List<string>();
            foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
            {
                ushort m = (ushort)b;
                if ((mask & m) != 0) names.Add(b.ToString());
            }
            return names.Count > 0 ? string.Join("+", names) : "(none)";
        }

        private void enabledCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (enabledCheckbox.Checked) RegisterHotkeys(); else UnregisterHotkeys();

            foreach (Label label in labels) label.Enabled = enabledCheckbox.Checked;
        }

        private void hotkeyLabelClicked(object sender, EventArgs e)
        {
            int j;
            for (int i = 0; i < textboxes.Length; i++) textboxes[i].Visible = false;
            for (j = 0; j < hotkeyLabels.Length && hotkeyLabels[j] != (Label)sender; j++) { }
            textboxes[j].Visible = true;
            textboxes[j].Focus();
        }

        private void hotkeyTextbox_TextChanged(object sender, EventArgs e)
        {
            TextBox changedTextBox = (TextBox)sender;
            if (changedTextBox.Text == "")
            {
                changedTextBox.Visible = false;
                return;
            }

            char newHotkey = char.ToUpper(changedTextBox.Text[0]);

            int i;
            for (i = 0; textboxes[i] != changedTextBox; i++) { }

            hotkeys[i].Key = newHotkey;
            hotkeys[i].Modifier = KeyModifiers.Shift;
            hotkeys[i].GamepadMask = null; // clear gamepad binding when keyboard rebind

            UnregisterHotkeys();
            RegisterHotkeys();

            hotkeyLabels[i].Text = hotkeys[i].ToString();
            textboxes[i].Visible = false;
            textboxes[i].Text = "";
        }

        /*
        private void gamepadAssignButton_Click(object sender, EventArgs e)
        {
            int idx = -1;
            if (sender == zeroLivesGamepadButton) idx = 0;
            else if (sender == giveMaskGamepadButton) idx = 1;

            if (idx >= 0 && idx < hotkeys.Length)
            {
                pendingGamepadIndex = idx;
                pendingAssignMask = 0;
                pendingAssignTimer?.Dispose();
                pendingAssignTimer = null;
                if (IsHandleCreated)
                {
                    if (InvokeRequired) BeginInvoke((Action)(() => hotkeyLabels[idx].Text = hotkeys[idx].ToString() + " (press controller)"));
                    else hotkeyLabels[idx].Text = hotkeys[idx].ToString() + " (press controller)";
                }
            }
            UpdateGamepadBindingsLabel();
        }

        private void gamepadClearButton_Click(object sender, EventArgs e)
        {
            int idx = -1;
            if (sender == zeroLivesClearButton) idx = 0;
            else if (sender == giveMaskClearButton) idx = 1;

            if (idx >= 0 && idx < hotkeys.Length)
            {
                hotkeys[idx].GamepadMask = null;
                // update labels
                hotkeyLabels[idx].Text = hotkeys[idx].ToString();
                UpdateGamepadBindingsLabel();
            }
        }
        */

        private void UpdateGamepadBindingsLabel()
        {
            if (!IsHandleCreated) return;
            var lines = new List<string>();
            for (int i = 0; i < hotkeys.Length; i++)
            {
                var hk = hotkeys[i];
                string binding = "(none)";
                if (hk.GamepadMask.HasValue && hk.GamepadMask.Value != 0)
                {
                    binding = MaskToNames(hk.GamepadMask.Value);
                }
                lines.Add(hk.Label + " => " + binding);
            }

            string text = string.Join(Environment.NewLine, lines);
            //if (InvokeRequired) BeginInvoke((Action)(() => gamepadBindingsLabel.Text = text)); else gamepadBindingsLabel.Text = text;
        }
    }
}
