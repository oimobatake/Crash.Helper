using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Crash.Helper.Input;

namespace Crash.Helper.Controls
{
    public partial class HotkeyControl : UserControl
    {
        private readonly HotkeyManager manager;
        private readonly HelperSettings draft;
        private readonly List<Hotkey> bindings;
        private readonly List<TextBox> editors = new List<TextBox>();
        private readonly Label status;

        internal HotkeyControl(HotkeyManager manager, HelperSettings draft)
        {
            this.manager = manager;
            this.draft = draft;
            bindings = manager.Hotkeys.Select(key => new Hotkey(key.Label, draft.Hotkeys[key.Label].Modifiers, draft.Hotkeys[key.Label].Key, key.Callback)).ToList();
            AutoSize = true;
            MinimumSize = new Size(480, 0);
            var box = new GroupBox { Text = "Hotkeys", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
            var header = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            var enabled = new CheckBox { Text = "Hotkeys enabled", Checked = draft.HotkeysEnabled, AutoSize = true };
            status = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 3, 3, 3) };
            enabled.CheckedChanged += (s, e) => draft.HotkeysEnabled = enabled.Checked;
            header.Controls.Add(enabled);
            header.Controls.Add(status);
            table.Controls.Add(header, 0, 0);
            table.SetColumnSpan(header, 3);
            for (int i = 0; i < bindings.Count; i++)
            {
                int index = i;
                var editor = new TextBox { ReadOnly = true, Width = 170, Text = bindings[i].ToString(), ShortcutsEnabled = false };
                editor.Enter += (s, e) => manager.SetEditing(true);
                editor.Leave += (s, e) => { editor.Text = bindings[index].ToString(); editor.Select(0, 0); manager.SetEditing(false); };
                editor.PreviewKeyDown += (s, e) => e.IsInputKey = true;
                editor.KeyDown += (s, e) => CaptureBinding(index, e);
                var reset = new Button { Text = "Reset", AutoSize = true };
                reset.Click += (s, e) => SetBinding(index, 0, KeyModifiers.None);
                editors.Add(editor);
                table.Controls.Add(new Label { Text = bindings[i].Label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, i + 1);
                table.Controls.Add(editor, 1, i + 1);
                table.Controls.Add(reset, 2, i + 1);
            }
            box.Controls.Add(table);
            Controls.Add(box);
            manager.StatusChanged += OnStatusChanged;
            OnStatusChanged(this, EventArgs.Empty);
        }

        private void OnStatusChanged(object sender, EventArgs e)
        {
            status.Text = manager.Status;
            status.ForeColor = manager.IsActive ? Color.ForestGreen : SystemColors.ControlDarkDark;
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
            modifiers |= KeyboardHotkeyListener.CurrentModifiers & KeyModifiers.Win;
            SetBinding(index, (uint)e.KeyCode, modifiers);
        }

        private void SetBinding(int index, uint key, KeyModifiers modifiers)
        {
            if (key != 0 && bindings.Where((binding, i) => i != index).Any(binding => binding.Key == key && binding.Modifier == modifiers))
            {
                MessageBox.Show(this, "This hotkey is already assigned.", "Hotkeys", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bindings[index].Key = key;
            bindings[index].Modifier = modifiers;
            draft.Hotkeys[bindings[index].Label] = new HotkeyBinding { Key = key, Modifiers = modifiers };
            editors[index].Text = bindings[index].ToString();
        }
    }
}
