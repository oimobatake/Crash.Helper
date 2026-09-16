using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Crash.Helper.Memory;

namespace Crash.Helper.Controls
{
    public partial class HotkeyControl : UserControl
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr handle, int id);
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int key);
        private readonly HelperSettings settings;
        private readonly CrashMemory memory;
        private readonly List<Hotkey> hotkeys = new List<Hotkey>();
        private readonly List<TextBox> editors = new List<TextBox>();
        private readonly Label status;
        private bool ready;
        private bool editing;
        public event EventHandler SettingsChanged;

        public HotkeyControl(CrashMemory memory, DataControl data, LevelSelectorControl levels, HelperSettings settings)
        {
            this.memory = memory;
            this.settings = settings;
            Add("Set lives to 0", () => data.SetLives(0));
            Add("Set lives to 99", () => data.SetLives(99));
            Add("+1 mask", () => { data.StoredMasks = memory.Masks.Read() + 1; data.Masks = data.StoredMasks; });
            Add("-1 mask", () => { data.StoredMasks = memory.Masks.Read() - 1; data.Masks = data.StoredMasks; });
            Add("Freeze lives", data.ToggleFreezeLives);
            Add("Freeze masks", data.ToggleFreezeMasks);
            Add("Freeze current level", data.ToggleCurrentLevel);
            Add("Level Lock / Stop Lock", levels.ToggleLock);
            Add("Previous level", () => levels.MoveSelection(-1));
            Add("Next level", () => levels.MoveSelection(1));
            Add("Launch Game", levels.LaunchSelectedLevel);

            AutoSize = true;
            MinimumSize = new Size(480, 0);
            var box = new GroupBox { Text = "Hotkeys", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
            var enabled = new CheckBox { Text = "Hotkeys enabled", Checked = settings.HotkeysEnabled, AutoSize = true };
            enabled.CheckedChanged += (s, e) => { settings.HotkeysEnabled = enabled.Checked; RegisterHotkeys(); SettingsChanged?.Invoke(this, EventArgs.Empty); };
            table.Controls.Add(enabled, 0, 0);
            table.SetColumnSpan(enabled, 3);
            for (int i = 0; i < hotkeys.Count; i++)
            {
                int index = i;
                var editor = new TextBox { ReadOnly = true, Width = 170, Text = BindingText(hotkeys[i]), ShortcutsEnabled = false };
                editor.Enter += (s, e) => { editing = true; UnregisterHotkeys(); };
                editor.Leave += (s, e) => { editing = false; RegisterHotkeys(); };
                editor.PreviewKeyDown += (s, e) => e.IsInputKey = true;
                editor.KeyDown += (s, e) => CaptureBinding(index, e);
                var clear = new Button { Text = "None", AutoSize = true };
                clear.Click += (s, e) => SetBinding(index, 0, KeyModifiers.None);
                editors.Add(editor);
                table.Controls.Add(new Label { Text = hotkeys[i].Label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, i + 1);
                table.Controls.Add(editor, 1, i + 1);
                table.Controls.Add(clear, 2, i + 1);
            }
            status = new Label { AutoSize = true, MaximumSize = new Size(470, 0) };
            table.Controls.Add(status, 0, hotkeys.Count + 1);
            table.SetColumnSpan(status, 3);
            box.Controls.Add(table);
            Controls.Add(box);
        }

        private void Add(string label, Action callback)
        {
            HotkeyBinding binding;
            if (!settings.Hotkeys.TryGetValue(label, out binding) || binding == null)
                settings.Hotkeys[label] = binding = new HotkeyBinding();
            hotkeys.Add(new Hotkey(label, binding.Modifiers, binding.Key, callback));
        }

        private static string BindingText(Hotkey key)
        {
            if (key.Key == 0) return "None";
            return (key.Modifier.HasFlag(KeyModifiers.Control) ? "Ctrl + " : "")
                + (key.Modifier.HasFlag(KeyModifiers.Alt) ? "Alt + " : "")
                + (key.Modifier.HasFlag(KeyModifiers.Shift) ? "Shift + " : "")
                + (key.Modifier.HasFlag(KeyModifiers.Win) ? "Win + " : "")
                + ((Keys)key.Key).ToString();
        }

        private void CaptureBinding(int index, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) return;
            var modifiers = KeyModifiers.None;
            if (e.Control) modifiers |= KeyModifiers.Control;
            if (e.Alt) modifiers |= KeyModifiers.Alt;
            if (e.Shift) modifiers |= KeyModifiers.Shift;
            if (GetKeyState((int)Keys.LWin) < 0 || GetKeyState((int)Keys.RWin) < 0) modifiers |= KeyModifiers.Win;
            SetBinding(index, (uint)e.KeyCode, modifiers);
        }

        private void SetBinding(int index, uint key, KeyModifiers modifiers)
        {
            if (key != 0 && hotkeys.Where((h, i) => i != index).Any(h => h.Key == key && h.Modifier == modifiers))
            {
                MessageBox.Show(this, "This hotkey is already assigned.", "Hotkeys", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            hotkeys[index].Key = key;
            hotkeys[index].Modifier = modifiers;
            settings.Hotkeys[hotkeys[index].Label] = new HotkeyBinding { Key = key, Modifiers = modifiers };
            editors[index].Text = BindingText(hotkeys[index]);
            RegisterHotkeys();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetReady(bool value)
        {
            ready = value;
            RegisterHotkeys();
        }

        public void EndEditing()
        {
            editing = false;
            RegisterHotkeys();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            BeginInvoke((Action)RegisterHotkeys);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotkeys();
            base.OnHandleDestroyed(e);
        }

        public void RegisterHotkeys()
        {
            UnregisterHotkeys();
            if (status == null) return;
            status.Text = ready ? "Click a binding and press a key combination." : "Hotkeys inactive: helper or game process unavailable.";
            if (!ready || editing || !settings.HotkeysEnabled) return;
            var failures = new List<string>();
            for (int i = 0; i < hotkeys.Count; i++)
                if (hotkeys[i].Key != 0 && !RegisterHotKey(Handle, i, (uint)hotkeys[i].Modifier | 0x4000, hotkeys[i].Key)) failures.Add(hotkeys[i].Label);
            if (failures.Count > 0) status.Text = "Hotkey unavailable: " + string.Join(", ", failures);
        }

        public void UnregisterHotkeys()
        {
            if (!IsHandleCreated) return;
            for (int i = 0; i < hotkeys.Count; i++) UnregisterHotKey(Handle, i);
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (message.Msg != 0x0312 || !ready || editing || !settings.HotkeysEnabled || !memory.ProcessHooked) return;
            int index = message.WParam.ToInt32();
            if (index >= 0 && index < hotkeys.Count) hotkeys[index].Callback();
        }
    }
}
