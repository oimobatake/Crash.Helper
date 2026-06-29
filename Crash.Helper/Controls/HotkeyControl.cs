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
using System.IO;
using System.Threading;
using System.Diagnostics;
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
        private bool hotkeysRegistered;
        private bool suppressEnabledCheckboxEvent;
        private bool userRequestedHotkeysEnabled = true;
        private int toggleLevelLockBusy;

        private string HotkeyConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hotkeys.config");

        public HotkeyControl(CrashMemory memory, DataControl data)
        {
            this.memory = memory;
            this.data = data;

            InitializeComponent();

            hotkeys = new[]
            {
                new Hotkey("Set Max lives: ", KeyModifiers.Shift, (uint)Keys.O, () => { memory.Lives.Write(99); data.Lives = 99; }),
                new Hotkey("Give one mask (+): ", KeyModifiers.Shift, (uint)Keys.F, () => { data.StoredMasks = memory.Masks.Read() + 1; data.Masks = data.StoredMasks; }),
                new Hotkey("Give one mask (-): ", KeyModifiers.Shift, (uint)Keys.D, () => { data.StoredMasks = memory.Masks.Read() - 1; data.Masks = data.StoredMasks; }),
                new Hotkey("Toggle level lock: ", KeyModifiers.Shift, (uint)Keys.L, QueueToggleLevelLock)
            };

            // Keep these ordered and in sync with designer controls
            labels = new[] { zeroLivesLabel, giveMaskLabel, label2, label1 };
            hotkeyLabels = new[] { zeroLivesHotkeyLabel, addMaskHotkeyLabel, subMaskHotkeyLabel, freezeLevelHotkeyLabel };
            textboxes = new[] { zeroLivesHotkeyTextbox, addMaskHotkeyTextbox, subMaskHotkeyTextbox, freezeLevelHotkeyTextbox };

            LoadHotkeySettings();
            RefreshHotkeyLabels();
            ApplyHotkeyActivationState();

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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyHotkeyActivationState();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            ApplyHotkeyActivationState();
        }

        private void RefreshHotkeyLabels()
        {
            for (int i = 0; i < hotkeyLabels.Length && i < hotkeys.Length; i++)
            {
                hotkeyLabels[i].Text = hotkeys[i].ToString();
                hotkeyLabels[i].ForeColor = Color.ForestGreen;
            }
        }

        private void LoadHotkeySettings()
        {
            try
            {
                userRequestedHotkeysEnabled = true;
                if (!File.Exists(HotkeyConfigPath))
                {
                    userRequestedHotkeysEnabled = enabledCheckbox.Checked;
                    return;
                }

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(HotkeyConfigPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    var key = line.Substring(0, split).Trim();
                    var value = line.Substring(split + 1).Trim();
                    values[key] = value;
                }

                if (values.TryGetValue("Enabled", out var enabledValue) && bool.TryParse(enabledValue, out var enabledParsed))
                {
                    userRequestedHotkeysEnabled = enabledParsed;
                }
                else
                {
                    userRequestedHotkeysEnabled = enabledCheckbox.Checked;
                }

                for (int i = 0; i < hotkeys.Length; i++)
                {
                    if (values.TryGetValue($"Hotkey{i}.Key", out var keyValue) && uint.TryParse(keyValue, out var keyParsed))
                    {
                        hotkeys[i].Key = keyParsed;
                    }

                    if (values.TryGetValue($"Hotkey{i}.Modifier", out var modifierValue) && int.TryParse(modifierValue, out var modifierParsed))
                    {
                        hotkeys[i].Modifier = (KeyModifiers)modifierParsed;
                    }

                    if (values.TryGetValue($"Hotkey{i}.GamepadMask", out var maskValue) && ushort.TryParse(maskValue, out var maskParsed))
                    {
                        hotkeys[i].GamepadMask = maskParsed;
                    }
                    else
                    {
                        hotkeys[i].GamepadMask = null;
                    }
                }
            }
            catch
            {
                userRequestedHotkeysEnabled = enabledCheckbox.Checked;
            }
        }

        private void SaveHotkeySettings()
        {
            try
            {
                var lines = new List<string>
                {
                    $"Enabled={userRequestedHotkeysEnabled}"
                };

                for (int i = 0; i < hotkeys.Length; i++)
                {
                    lines.Add($"Hotkey{i}.Key={hotkeys[i].Key}");
                    lines.Add($"Hotkey{i}.Modifier={(int)hotkeys[i].Modifier}");
                    lines.Add($"Hotkey{i}.GamepadMask={(hotkeys[i].GamepadMask.HasValue ? hotkeys[i].GamepadMask.Value.ToString() : string.Empty)}");
                }

                File.WriteAllLines(HotkeyConfigPath, lines);
            }
            catch { }
        }

        private bool IsRuntimeReady()
        {
            try
            {
                return this.Enabled && memory != null && memory.ProcessHooked;
            }
            catch
            {
                return false;
            }
        }

        private bool IsHotkeyExecutionEnabled()
        {
            return IsRuntimeReady() && userRequestedHotkeysEnabled;
        }

        private void ApplyHotkeyActivationState()
        {
            bool runtimeReady = IsRuntimeReady();
            bool shouldEnable = runtimeReady && userRequestedHotkeysEnabled;

            if (enabledCheckbox != null)
            {
                try
                {
                    suppressEnabledCheckboxEvent = true;
                    enabledCheckbox.Enabled = runtimeReady;
                    enabledCheckbox.Checked = shouldEnable;
                }
                finally
                {
                    suppressEnabledCheckboxEvent = false;
                }
            }

            if (shouldEnable) RegisterHotkeys(); else UnregisterHotkeys();

            bool labelsEnabled = runtimeReady;
            if (labels != null)
            {
                foreach (Label label in labels)
                {
                    if (label != null) label.Enabled = labelsEnabled;
                }
            }
        }

        private void QueueToggleLevelLock()
        {
            if (Interlocked.CompareExchange(ref toggleLevelLockBusy, 1, 0) != 0)
            {
                return;
            }

            try
            {
                if (!IsHandleCreated)
                {
                    Interlocked.Exchange(ref toggleLevelLockBusy, 0);
                    return;
                }

                BeginInvoke((Action)(() =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var levelSelector = this.Parent?.Controls.OfType<LevelSelectorControl>().FirstOrDefault();
                        if (levelSelector != null)
                        {
                            if (levelSelector.IsLevelLockActive)
                            {
                                levelSelector.StopLevelLock();
                            }
                            else
                            {
                                levelSelector.ApplySelectedLevelLock();
                            }
                            return;
                        }

                        if (data.IsMapFrozen) data.StopMapLock();
                        else
                        {
                            var mapVal = memory.LoadMap.Read();
                            var mapKey = LevelSelectorControl.Levels.Keys.FirstOrDefault(k => LevelSelectorControl.Levels[k] == mapVal);
                            if (!string.IsNullOrEmpty(mapVal)) data.SetMapLock(mapVal, mapKey, true);
                        }
                    }
                    catch { }
                    finally
                    {
                        sw.Stop();
                        Trace.WriteLine($"[Hotkey] Toggle Level Lock executed in {sw.ElapsedMilliseconds}ms");
                        Interlocked.Exchange(ref toggleLevelLockBusy, 0);
                    }
                }));
            }
            catch
            {
                Interlocked.Exchange(ref toggleLevelLockBusy, 0);
            }
        }

        private void Data_MapLockChanged(object sender, string mapValue)
        {
            // when map value changes, update LevelSelector combo selection
            try
            {
                if (data == null) return;
                if (data.InvokeRequired)
                {
                    data.BeginInvoke((Action)(() => Data_MapLockChanged(sender, mapValue)));
                    return;
                }

                var ls = this.Parent?.Controls.OfType<LevelSelectorControl>().FirstOrDefault();
                if (ls == null) return;
                ls.SyncSelectionByMap(mapValue);
            }
            catch { }
        }

        public void RegisterHotkeys()
        {
            if (!IsHandleCreated || hotkeysRegistered) return;

            int keyCount = 0;
            foreach (Hotkey hotkey in hotkeys)
            {
                RegisterHotKey(Handle, keyCount++, (uint)hotkey.Modifier, hotkey.Key);
            }

            hotkeysRegistered = true;
        }

        public void UnregisterHotkeys()
        {
            if (!IsHandleCreated || !hotkeysRegistered) return;

            for (int i = 0; i < hotkeys.Length; i++)
            {
                UnregisterHotKey(Handle, i);
            }

            hotkeysRegistered = false;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg != 0x0312) return;
            if (!IsHotkeyExecutionEnabled()) return;

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
            if (!IsHotkeyExecutionEnabled()) return;
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
                SaveHotkeySettings();
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
            if (suppressEnabledCheckboxEvent) return;

            userRequestedHotkeysEnabled = enabledCheckbox.Checked;
            SaveHotkeySettings();
            ApplyHotkeyActivationState();
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

            SaveHotkeySettings();
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
