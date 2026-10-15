using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Globalization;
using Crash.Helper.Input;

namespace Crash.Helper.Controls
{
    public partial class HotkeyControl : UserControl
    {
        private sealed class BindingRow
        {
            internal string Label;
            internal HotkeyBinding Binding;
            internal HotkeyTextBox Editor;
            internal Button Add;
            internal TableLayoutPanel Table;
            internal Button Reset, Remove;
            internal bool IsAlternative, Removed;
        }

        private readonly HotkeyManager manager;
        private readonly HelperSettings draft;
        private readonly List<BindingRow> rows = new List<BindingRow>();
        private readonly List<TextBox> editors = new List<TextBox>();
        private readonly List<GroupBox> bindingGroups = new List<GroupBox>();
        private readonly List<Control> buildingLayouts = new List<Control>();
        private readonly Label status;
        private readonly Dictionary<string, TextBox> speedEditors = new Dictionary<string, TextBox>();
        private int gamepadEditor = -1;
        private uint gamepadChord;
        private bool fixedLayout;
        private bool fontReflowPending, deferReflow, freezePending;

        internal HotkeyControl(HotkeyManager manager, HelperSettings draft)
        {
            this.manager = manager;
            this.draft = draft;
            HoldLayout(this);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(580, 0);
            var box = HoldLayout(new GroupBox { Text = "Hotkeys", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) });
            var groups = HoldLayout(new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill });
            var header = HoldLayout(new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty });
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
            foreach (var action in manager.Hotkeys)
            {
                if (action.Group != currentGroup)
                {
                    currentGroup = action.Group;
                    var group = HoldLayout(new GroupBox { Text = currentGroup, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(6), Margin = new Padding(0, 4, 0, 4), Enabled = draft.HotkeysEnabled });
                    bindingGroups.Add(group);
                    table = HoldLayout(new TableLayoutPanel { AutoSize = true, ColumnCount = 4, Dock = DockStyle.Fill });
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    for (int column = 1; column < 4; column++) table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    group.Controls.Add(table);
                    groups.Controls.Add(group, 0, groups.Controls.Count);
                    row = 0;
                    if (currentGroup == "Position") AddSpeedEditor(table, ref row, "XYZ Speed", nameof(HelperSettings.PositionXYZSpeed));
                    if (currentGroup == "Camera")
                    {
                        var followPitch = new CheckBox { Text = "Move in the pitch direction", AutoSize = true, Checked = draft.CameraMoveWithPitch };
                        var mouse = new CheckBox { Text = "Mouse control", AutoSize = true, Checked = draft.CameraMouseControl };
                        var invertX = new CheckBox { Text = "Invert mouse X", AutoSize = true, Checked = draft.CameraInvertMouseX };
                        var invert = new CheckBox { Text = "Invert mouse Y", AutoSize = true, Checked = draft.CameraInvertMouseY };
                        followPitch.CheckedChanged += (s, e) => draft.CameraMoveWithPitch = followPitch.Checked;
                        mouse.CheckedChanged += (s, e) => draft.CameraMouseControl = mouse.Checked;
                        invertX.CheckedChanged += (s, e) => draft.CameraInvertMouseX = invertX.Checked;
                        invert.CheckedChanged += (s, e) => draft.CameraInvertMouseY = invert.Checked;
                        foreach (var checkbox in new[] { followPitch, mouse, invertX, invert })
                        { table.Controls.Add(checkbox, 0, row++); table.SetColumnSpan(checkbox, 4); }
                        AddSpeedEditor(table, ref row, "XYZ Speed", nameof(HelperSettings.CameraXYZSpeed));
                        AddSpeedEditor(table, ref row, "YawPitch Speed", nameof(HelperSettings.CameraYawPitchSpeed));
                        AddSpeedEditor(table, ref row, "Mouse X sensitivity", nameof(HelperSettings.CameraMouseXSensitivity));
                        AddSpeedEditor(table, ref row, "Mouse Y sensitivity", nameof(HelperSettings.CameraMouseYSensitivity));
                        AddSpeedEditor(table, ref row, "Orbit Horizontal (degrees)", nameof(HelperSettings.CameraOrbitHorizontalStep));
                        AddSpeedEditor(table, ref row, "Orbit Vertical (degrees)", nameof(HelperSettings.CameraOrbitVerticalStep));
                        AddSpeedEditor(table, ref row, "Follow scale", nameof(HelperSettings.CameraFollowScale));
                        AddSpeedEditor(table, ref row, "Follow vertical deadzone", nameof(HelperSettings.CameraFollowVerticalDeadzone));
                        AddSpeedEditor(table, ref row, "Follow speed (0 = instant)", nameof(HelperSettings.CameraFollowSpeed));
                    }
                }
                var binding = draft.Hotkeys[action.Label];
                var inputs = HoldLayout(new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill, Margin = Padding.Empty });
                for (int column = 0; column < 3; column++) inputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                table.Controls.Add(new Label { Text = action.Label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(3, 7, 3, 3) }, 0, row);
                table.Controls.Add(inputs, 1, row++);
                table.SetColumnSpan(inputs, 3);
                var add = new Button { Text = "Add key", AutoSize = true, Enabled = binding.IsAssigned };
                AddBindingRow(action.Label, binding, inputs, add);
                if (binding.Alternatives != null)
                    foreach (var alternative in binding.Alternatives.Where(value => value != null)) AddBindingRow(action.Label, alternative, inputs, add);
                inputs.Controls.Add(add, 2, 0);
                add.Click += (s, e) =>
                {
                    if (!binding.IsAssigned) return;
                    var alternative = new HotkeyBinding();
                    if (binding.Alternatives == null) binding.Alternatives = new List<HotkeyBinding>();
                    binding.Alternatives.Add(alternative);
                    AddBindingRow(action.Label, alternative, inputs, add).Editor.Focus();
                };
            }
            box.Controls.Add(groups);
            Controls.Add(box);
            manager.StatusChanged += OnStatusChanged;
            manager.GamepadButtonsChanged += OnGamepadButtonsChanged;
            Disposed += (s, e) =>
            {
                manager.StatusChanged -= OnStatusChanged;
                manager.GamepadButtonsChanged -= OnGamepadButtonsChanged;
            };
            OnStatusChanged(this, EventArgs.Empty);
            for (int i = buildingLayouts.Count - 1; i >= 0; i--) buildingLayouts[i].ResumeLayout(true);
            QueueFreezeLayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            QueueFreezeLayout();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            bool hold = fixedLayout;
            if (hold) foreach (var control in buildingLayouts) control.SuspendLayout();
            try { base.OnVisibleChanged(e); }
            finally
            {
                if (hold)
                    for (int i = buildingLayouts.Count - 1; i >= 0; i--) buildingLayouts[i].ResumeLayout(false);
            }
            if (Visible) QueueFreezeLayout();
        }

        private void QueueFreezeLayout()
        {
            if (fixedLayout || freezePending || !IsHandleCreated || buildingLayouts == null || buildingLayouts.Count == 0) return;
            freezePending = true;
            BeginInvoke(new Action(() =>
            {
                freezePending = false;
                if (!IsDisposed && Visible) FreezeLayout();
            }));
        }

        // The rows have fixed inputs and fonts. Cache their final container sizes instead of
        // recursively autosizing hundreds of controls on every tab visibility change.
        private void FreezeLayout()
        {
            foreach (var control in buildingLayouts) control.SuspendLayout();
            for (int i = buildingLayouts.Count - 1; i >= 0; i--)
            {
                var control = buildingLayouts[i];
                Size size = control.Size;
                control.AutoSize = false;
                control.Size = size;
            }
            for (int i = buildingLayouts.Count - 1; i >= 0; i--) buildingLayouts[i].ResumeLayout(false);
            fixedLayout = true;
            // Disabling AutoSize resets bounds of docked children. Place every table's cells
            // once with fixed sizes before caching the layout for future tab switches.
            for (int i = buildingLayouts.Count - 1; i >= 0; i--) buildingLayouts[i].PerformLayout();
            Parent?.PerformLayout();
        }

        private void ReflowBindings()
        {
            if (!fixedLayout || deferReflow) return;
            foreach (var control in buildingLayouts) { control.SuspendLayout(); control.AutoSize = true; }
            for (int i = buildingLayouts.Count - 1; i >= 0; i--) buildingLayouts[i].ResumeLayout(true);
            Parent?.PerformLayout();
            FreezeLayout();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (!fixedLayout || !IsHandleCreated || fontReflowPending) return;
            fontReflowPending = true;
            BeginInvoke(new Action(() =>
            {
                fontReflowPending = false;
                if (!IsDisposed) ReflowBindings();
            }));
        }

        private T HoldLayout<T>(T control) where T : Control
        {
            control.SuspendLayout();
            if (control is Panel panel) panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buildingLayouts.Add(control);
            return control;
        }

        private BindingRow AddBindingRow(string label, HotkeyBinding binding, TableLayoutPanel table, Button add)
        {
            int index = rows.Count;
            var row = new BindingRow { Label = label, Binding = binding, Add = add, Table = table,
                IsAlternative = !ReferenceEquals(binding, draft.Hotkeys[label]) };
            var editor = new HotkeyTextBox { ReadOnly = true, Width = 170, Text = Display(binding), ShortcutsEnabled = false };
            row.Editor = editor;
            editor.Enter += (s, e) => { gamepadEditor = index; gamepadChord = 0; manager.SetEditing(true); };
            editor.Leave += (s, e) => { if (gamepadEditor == index) gamepadEditor = -1; editor.Text = Display(binding); editor.Select(0, 0); manager.SetEditing(false); };
            editor.PreviewKeyDown += (s, e) => e.IsInputKey = true;
            editor.KeyDown += (s, e) => CaptureBinding(index, e);
            var reset = new Button { Text = "Reset", AutoSize = true };
            row.Reset = reset;
            reset.Click += (s, e) => SetBinding(index, 0, KeyModifiers.None);
            rows.Add(row);
            editors.Add(editor);
            int number = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(editor, 0, number);
            table.Controls.Add(reset, 1, number);
            if (row.IsAlternative)
            {
                row.Remove = new Button { Text = "Remove key", AutoSize = true };
                row.Remove.Click += (s, e) => RemoveBindingRow(row);
                table.Controls.Add(row.Remove, 2, number);
            }
            ReflowBindings();
            return row;
        }

        private void RemoveBindingRow(BindingRow row)
        {
            if (!row.IsAlternative || row.Removed) return;
            row.Removed = true;
            if (gamepadEditor == rows.IndexOf(row)) { gamepadEditor = -1; gamepadChord = 0; manager.SetEditing(false); }
            draft.Hotkeys[row.Label].Alternatives?.Remove(row.Binding);
            var table = row.Table;
            table.SuspendLayout();
            try
            {
                int number = table.GetRow(row.Editor);
                foreach (var control in new Control[] { row.Editor, row.Reset, row.Remove })
                { table.Controls.Remove(control); control.Dispose(); }
                foreach (Control control in table.Controls)
                {
                    int current = table.GetRow(control);
                    if (current > number) table.SetRow(control, current - 1);
                }
                table.RowStyles.RemoveAt(number);
                table.RowCount--;
            }
            finally { table.ResumeLayout(true); }
            ReflowBindings();
        }

        internal void RemoveEmptyBindings()
        {
            deferReflow = true;
            try
            {
                foreach (var row in rows.Where(row => row.IsAlternative && !row.Removed && !row.Binding.IsAssigned).ToArray())
                    RemoveBindingRow(row);
            }
            finally { deferReflow = false; ReflowBindings(); }
        }

        private static string Display(HotkeyBinding binding) =>
            new Hotkey(string.Empty, binding.Modifiers, binding.Key, null) { GamepadMask = binding.Gamepad }.ToString();

        private void RefreshBinding(int index)
        {
            var row = rows[index];
            row.Editor.Text = Display(row.Binding);
            row.Add.Enabled = draft.Hotkeys[row.Label].IsAssigned;
        }

        private void AddSpeedEditor(TableLayoutPanel table, ref int row, string title, string setting)
        {
            var property = typeof(HelperSettings).GetProperty(setting);
            var label = new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left };
            var editor = new TextBox { Width = 170, TextAlign = HorizontalAlignment.Right, Text = ((float)property.GetValue(draft)).ToString("R", CultureInfo.InvariantCulture), Tag = label };
            editor.Enter += (s, e) => manager.SetEditing(true);
            editor.Leave += (s, e) => { editor.Select(0, 0); manager.SetEditing(false); };
            editor.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ValidateSpeeds(); } };
            var reset = new Button { Text = "Default", AutoSize = true };
            reset.Click += (s, e) =>
            {
                var value = (float)property.GetValue(new HelperSettings());
                editor.Text = value.ToString("R", CultureInfo.InvariantCulture);
                property.SetValue(draft, value);
            };
            speedEditors.Add(setting, editor);
            table.Controls.Add(label, 0, row);
            table.Controls.Add(editor, 1, row);
            table.Controls.Add(reset, 2, row++);
        }

        internal bool ValidateSpeeds()
        {
            foreach (var entry in speedEditors)
            {
                float value;
                if (!float.TryParse(entry.Value.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || value < 0 || value > 1000)
                {
                    MessageBox.Show(this, "Enter a finite number between 0 and 1000 for " + ((Label)entry.Value.Tag).Text + ".", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    entry.Value.Focus(); return false;
                }
                typeof(HelperSettings).GetProperty(entry.Key).SetValue(draft, value);
            }
            return true;
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
            gamepadChord = 0;
        }

        private void SetBinding(int index, uint key, KeyModifiers modifiers)
        {
            var binding = rows[index].Binding;
            binding.Key = key;
            binding.Modifiers = modifiers;
            binding.Gamepad = 0;
            RefreshBinding(index);
        }

        // Capture a controller chord on release, replacing the entire input in this row.
        private void OnGamepadButtonsChanged(uint buttons)
        {
            if (gamepadEditor < 0 || !editors[gamepadEditor].Focused) { gamepadChord = 0; return; }
            gamepadChord |= buttons;
            if (buttons != 0 || gamepadChord == 0) return;
            SetGamepadBinding(gamepadEditor, gamepadChord);
            gamepadChord = 0;
        }

        private void SetGamepadBinding(int index, uint mask)
        {
            var binding = rows[index].Binding;
            binding.Key = 0;
            binding.Modifiers = KeyModifiers.None;
            binding.Gamepad = mask;
            RefreshBinding(index);
        }
    }
}
