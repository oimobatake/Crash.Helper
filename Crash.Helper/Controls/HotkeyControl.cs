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
        private readonly List<GroupBox> bindingGroups = new List<GroupBox>();
        private readonly Label status;

        internal HotkeyControl(HotkeyManager manager, HelperSettings draft)
        {
            this.manager = manager;
            this.draft = draft;
            bindings = manager.Hotkeys.Select(key => new Hotkey(key.Label, draft.Hotkeys[key.Label].Modifiers, draft.Hotkeys[key.Label].Key, key.Callback) { Group = key.Group }).ToList();
            AutoSize = true;
            MinimumSize = new Size(424, 0);
            var box = new GroupBox { Text = "Hotkeys", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
            var groups = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
            var header = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            var enabled = new CheckBox { Text = "Hotkeys enabled", Checked = draft.HotkeysEnabled, AutoSize = true };
            status = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 3, 3, 3) };
            enabled.CheckedChanged += (s, e) =>
            {
                draft.HotkeysEnabled = enabled.Checked;
                foreach (var group in bindingGroups) group.Enabled = enabled.Checked;
            };
            header.Controls.Add(enabled);
            header.Controls.Add(status);
            groups.Controls.Add(header, 0, 0);
            TableLayoutPanel table = null;
            string currentGroup = null;
            int row = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].Group != currentGroup)
                {
                    currentGroup = bindings[i].Group;
                    var group = new GroupBox { Text = currentGroup, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(6), Margin = new Padding(0, 4, 0, 4), Enabled = draft.HotkeysEnabled };
                    bindingGroups.Add(group);
                    table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    group.Controls.Add(table);
                    groups.Controls.Add(group, 0, groups.Controls.Count);
                    row = 0;
                    if (currentGroup == "Camera")
                    {
                        var followPitch = new CheckBox { Text = "Move in the pitch direction", AutoSize = true, Checked = draft.CameraMoveWithPitch };
                        var mouse = new CheckBox { Text = "Mouse control", AutoSize = true, Checked = draft.CameraMouseControl };
                        followPitch.CheckedChanged += (s, e) => draft.CameraMoveWithPitch = followPitch.Checked;
                        mouse.CheckedChanged += (s, e) => draft.CameraMouseControl = mouse.Checked;
                        table.Controls.Add(followPitch, 0, row++); table.SetColumnSpan(followPitch, 3);
                        table.Controls.Add(mouse, 0, row++); table.SetColumnSpan(mouse, 3);
                    }
                }
                int index = i;
                var editor = new HotkeyTextBox { ReadOnly = true, Width = 170, Text = bindings[i].ToString(), ShortcutsEnabled = false };
                editor.Enter += (s, e) => manager.SetEditing(true);
                editor.Leave += (s, e) => { editor.Text = bindings[index].ToString(); editor.Select(0, 0); manager.SetEditing(false); };
                editor.PreviewKeyDown += (s, e) => e.IsInputKey = true;
                editor.KeyDown += (s, e) => CaptureBinding(index, e);
                var reset = new Button { Text = "Reset", AutoSize = true };
                reset.Click += (s, e) => SetBinding(index, 0, KeyModifiers.None);
                editors.Add(editor);
                table.Controls.Add(new Label { Text = bindings[i].Label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
                table.Controls.Add(editor, 1, row);
                table.Controls.Add(reset, 2, row);
                row++;
            }
            box.Controls.Add(groups);
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
            var modifiers = KeyModifiers.None;
            if (e.Control) modifiers |= KeyModifiers.Control;
            if (e.Alt) modifiers |= KeyModifiers.Alt;
            if (e.Shift) modifiers |= KeyModifiers.Shift;
            modifiers |= KeyboardHotkeyListener.CurrentModifiers & KeyModifiers.Win;
            uint key = (uint)e.KeyCode;
            if (e.KeyCode == Keys.Enter && ((HotkeyTextBox)editors[index]).PhysicalKey == KeyIdentity.NumEnter) key = KeyIdentity.NumEnter;
            key = KeyIdentity.Normalize(key);
            SetBinding(index, key, KeyIdentity.ModifiersForKey(key, modifiers));
        }

        private void SetBinding(int index, uint key, KeyModifiers modifiers)
        {
            bindings[index].Key = key;
            bindings[index].Modifier = modifiers;
            draft.Hotkeys[bindings[index].Label] = new HotkeyBinding { Key = key, Modifiers = modifiers };
            editors[index].Text = bindings[index].ToString();
        }
    }
}
