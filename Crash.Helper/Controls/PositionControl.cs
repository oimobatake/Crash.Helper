using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Crash.Helper.Memory;

namespace Crash.Helper.Controls
{
    internal sealed class PositionControl : UserControl
    {
        private readonly CrashMemory memory;
        private readonly TextBox[] editors = new TextBox[3];
        private readonly string[] axes = { "X", "Y", "Z" };
        private readonly GamePointer<float>[] pointers;
        private readonly Button teleportButton;
        private float[] savedPosition;
        private readonly CheckBox[] freezeCheckboxes = new CheckBox[3];
        private readonly float[] frozenValues = new float[3];
        private readonly Timer freezeTimer = new Timer { Interval = 10 };

        public PositionControl(CrashMemory memory)
        {
            this.memory = memory;
            pointers = new[] { memory.PositionX, memory.PositionY, memory.PositionZ };
            Size = new Size(270, 112);
            for (int i = 0; i < editors.Length; i++)
            {
                int index = i;
                var label = new Label { Text = "Position " + axes[i] + ":", AutoSize = true, Left = 12, Top = i * 26 + 4 };
                var editor = new TextBox { Left = 80, Top = i * 26, Width = 95, Text = "-", TextAlign = HorizontalAlignment.Right };
                var freeze = new CheckBox { Text = "Freeze " + axes[i], AutoSize = true, Left = 190, Top = i * 26 + 2 };
                freeze.CheckedChanged += (s, e) =>
                {
                    if (freeze.Checked && Enabled && memory.ProcessHooked) frozenValues[index] = pointers[index].Read();
                    UpdateFreezeTimer();
                };
                freezeCheckboxes[i] = freeze;
                Controls.Add(label);
                Controls.Add(freeze);
                editor.KeyDown += (s, e) =>
                {
                    if (e.KeyCode != Keys.Enter) return;
                    e.SuppressKeyPress = true;
                    if (!Enabled || !memory.ProcessHooked) return;
                    float value;
                    if (!TryParseCoordinate(editor.Text, axes[index], out value))
                    {
                        MessageBox.Show(this, "Enter a finite number for " + axes[index] + ".", "Position", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    frozenValues[index] = value;
                    pointers[index].Write(value);
                    editor.Text = FormatCoordinate(value);
                    editor.Select(0, 0);
                    var form = FindForm();
                    if (form != null) form.ActiveControl = null;
                };
                editor.Leave += (s, e) => { editor.Select(0, 0); UpdateEditor(index); };
                editors[i] = editor;
                Controls.Add(editor);
            }
            var save = new Button { Text = "Save", Left = 52, Top = 80, Width = 80 };
            teleportButton = new Button { Text = "TP", Left = 137, Top = 80, Width = 80, Enabled = false };
            save.Click += (s, e) => SavePosition();
            teleportButton.Click += (s, e) => Teleport();
            Controls.Add(save);
            Controls.Add(teleportButton);
            freezeTimer.Tick += (s, e) => FreezeCoordinates();
            EnabledChanged += (s, e) => UpdateFreezeTimer();
        }

        internal static bool TryParseCoordinate(string text, string axis, out float value)
        {
            text = text.Trim();
            if (text.StartsWith(axis + ":", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2).Trim();
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // Framework Single formatting rounds to seven significant digits before padding F8.
        // Widen only for display so the stored float's decimal places are retained.
        private static string FormatCoordinate(float value) => ((double)value).ToString("F8", CultureInfo.InvariantCulture);

        private void UpdateEditor(int index)
        {
            editors[index].Text = Enabled && memory.ProcessHooked ? FormatCoordinate(pointers[index].Read()) : "-";
        }

        public void RefreshValues()
        {
            for (int i = 0; i < editors.Length; i++) if (!editors[i].Focused) UpdateEditor(i);
        }

        internal void SavePosition()
        {
            if (!Enabled || !memory.ProcessHooked) return;
            savedPosition = new[] { pointers[0].Read(), pointers[1].Read(), pointers[2].Read() };
            teleportButton.Enabled = true;
        }

        internal void Teleport()
        {
            if (!Enabled || !memory.ProcessHooked || savedPosition == null) return;
            for (int i = 0; i < pointers.Length; i++)
            {
                frozenValues[i] = savedPosition[i];
                pointers[i].Write(savedPosition[i]);
            }
            RefreshValues();
        }
        internal void ToggleFreeze(int axis)
        {
            if (Enabled && memory.ProcessHooked) freezeCheckboxes[axis].Checked = !freezeCheckboxes[axis].Checked;
        }

        private void UpdateFreezeTimer()
        {
            if (Enabled && memory.ProcessHooked && Array.Exists(freezeCheckboxes, box => box != null && box.Checked)) freezeTimer.Start();
            else freezeTimer.Stop();
        }

        internal void FreezeCoordinates()
        {
            if (!Enabled || !memory.ProcessHooked) { freezeTimer.Stop(); return; }
            for (int i = 0; i < pointers.Length; i++)
                if (freezeCheckboxes[i].Checked) pointers[i].Write(frozenValues[i]);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) freezeTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
