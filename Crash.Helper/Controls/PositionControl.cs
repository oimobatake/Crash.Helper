using System;
using System.Drawing;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Memory;
using Crash.Helper.Memory.Camera;
using Crash.Helper.Memory.Position;

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
        private readonly PositionPatchService patchService = new PositionPatchService();
        private readonly Stopwatch movementClock = Stopwatch.StartNew();
        private readonly TextBox xyzSpeedEditor;
        private readonly Label speedLabel;
        private HelperSettings settings;
        private Func<float[]> viewOrientation;
        private int[] heldDirections = new int[3];
        private bool inputEnabled, closing, requestedPatch;
        private bool speedBoost;
        private Process patchProcess;
        private int patchOffset;
        private double previousMovementTime;
        internal event Action<bool> EditingChanged;

        public PositionControl(CrashMemory memory)
        {
            this.memory = memory;
            pointers = new[] { memory.PositionX, memory.PositionY, memory.PositionZ };
            Size = new Size(270, 144);
            for (int i = 0; i < editors.Length; i++)
            {
                int index = i;
                var label = new Label { Text = axes[i] + ":", AutoSize = true, Left = 12, Top = i * 26 + 4 };
                var editor = new TextBox { Left = 48, Top = i * 26, Width = 100, Text = "-", TextAlign = HorizontalAlignment.Right };
                var freeze = new CheckBox { Text = "Freeze " + axes[i], AutoSize = true, Left = 160, Top = i * 26 + 2 };
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
                    if (e.KeyCode != Keys.Enter) { EditingChanged?.Invoke(true); return; }
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
                editor.Enter += (s, e) => EditingChanged?.Invoke(true);
                editor.MouseDown += (s, e) => EditingChanged?.Invoke(true);
                editor.Leave += (s, e) => { editor.Select(0, 0); UpdateEditor(index); EditingChanged?.Invoke(false); };
                editors[i] = editor;
                Controls.Add(editor);
            }
            speedLabel = new Label { Text = "XYZ Speed:", AutoSize = true, Left = 12, Top = 84 };
            xyzSpeedEditor = new TextBox { Left = 150, Top = 80, Width = 80, Text = "0.8", TextAlign = HorizontalAlignment.Right };
            xyzSpeedEditor.Enter += (s, e) => EditingChanged?.Invoke(true);
            xyzSpeedEditor.MouseDown += (s, e) => EditingChanged?.Invoke(true);
            xyzSpeedEditor.Leave += (s, e) => { ResetSpeedEditor(); EditingChanged?.Invoke(false); };
            xyzSpeedEditor.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) { EditingChanged?.Invoke(true); return; }
                e.SuppressKeyPress = true;
                float value;
                try
                {
                    if (!float.TryParse(xyzSpeedEditor.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        throw new ArgumentException("Enter a finite speed between 0 and 1000.");
                    settings.SaveMovementValue(nameof(HelperSettings.PositionXYZSpeed), value);
                }
                catch (Exception ex) { HelperLog.Error("Save position speed", ex); }
                ResetSpeedEditor();
                if (FindForm() != null) FindForm().ActiveControl = null;
                EditingChanged?.Invoke(false);
            };
            Controls.Add(speedLabel); Controls.Add(xyzSpeedEditor);
            var save = new Button { Text = "Save", Left = 52, Top = 112, Width = 80 };
            teleportButton = new Button { Text = "TP", Left = 137, Top = 112, Width = 80, Enabled = false };
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
            UpdateFreezeTimer();
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
            bool available = Enabled && memory.ProcessHooked && !closing;
            bool any = Array.Exists(freezeCheckboxes, box => box != null && box.Checked);
            if (available && any)
            {
                if (!freezeTimer.Enabled) previousMovementTime = movementClock.Elapsed.TotalSeconds;
                freezeTimer.Start();
            }
            else freezeTimer.Stop();
            if (xyzSpeedEditor != null) xyzSpeedEditor.Enabled = speedLabel.Enabled = available && any;
            UpdatePatch(available ? memory.PositionX.Process : null, available ? memory.Profile?.PositionCodeOffset ?? 0 : 0,
                available && freezeCheckboxes.All(box => box != null && box.Checked));
        }

        private async void UpdatePatch(Process target, int offset, bool enabled)
        {
            if (closing || (ReferenceEquals(target, patchProcess) && offset == patchOffset && enabled == requestedPatch)) return;
            patchProcess = target; patchOffset = offset; requestedPatch = enabled;
            try { await patchService.ConfigureAsync(target, offset, enabled); }
            catch (Exception ex) { HelperLog.Error("Configure player position instructions", ex); }
        }

        internal void ApplySettings(HelperSettings settings, Func<float[]> orientation)
        {
            this.settings = settings;
            viewOrientation = orientation;
            if (!xyzSpeedEditor.Focused) ResetSpeedEditor();
        }

        private void ResetSpeedEditor()
        {
            xyzSpeedEditor.Text = (settings?.PositionXYZSpeed ?? 0.8f).ToString("R", CultureInfo.InvariantCulture);
            xyzSpeedEditor.Select(0, 0);
        }

        internal void SetMovement(int[] directions)
        {
            heldDirections = (int[])directions.Clone();
            previousMovementTime = movementClock.Elapsed.TotalSeconds;
        }

        internal void SetSpeedBoost(bool enabled) => speedBoost = enabled;

        internal void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled) { heldDirections = new int[3]; speedBoost = false; }
            previousMovementTime = movementClock.Elapsed.TotalSeconds;
        }

        internal void FreezeCoordinates()
        {
            if (!Enabled || !memory.ProcessHooked || closing) { UpdateFreezeTimer(); return; }
            double now = movementClock.Elapsed.TotalSeconds;
            double elapsed = now - previousMovementTime;
            previousMovementTime = now;
            if (inputEnabled && heldDirections.Any(value => value != 0))
            {
                var orientation = viewOrientation?.Invoke();
                if (orientation != null)
                {
                    var directions = new int[5]; heldDirections.CopyTo(directions, 0);
                    var delta = CameraMovement.Delta(directions, settings.PositionXYZSpeed * (speedBoost ? 2 : 1), 0, elapsed,
                        orientation[0]);
                    for (int i = 0; i < 3; i++)
                        if (freezeCheckboxes[i].Checked && delta[i].HasValue)
                        {
                            float value = frozenValues[i] + delta[i].Value;
                            if (!float.IsNaN(value) && !float.IsInfinity(value)) frozenValues[i] = value;
                        }
                }
            }
            for (int i = 0; i < pointers.Length; i++)
                if (freezeCheckboxes[i].Checked) pointers[i].Write(frozenValues[i]);
        }

        internal async Task ShutdownAsync()
        {
            closing = true; inputEnabled = false; freezeTimer.Stop();
            await patchService.ConfigureAsync(null, 0, false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { closing = true; freezeTimer.Dispose(); patchService.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
