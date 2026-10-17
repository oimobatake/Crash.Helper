using System;
using System.Drawing;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Memory;
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
        private readonly PositionMotionService motion = new PositionMotionService();
        private readonly PositionPatchService patchService = new PositionPatchService();
        private HelperSettings settings;
        private Func<float[]> viewOrientation;
        private int[] heldDirections = new int[3];
        private readonly HoldButton[,] movementButtons = new HoldButton[3, 2];
        private readonly bool[,] buttonHeld = new bool[3, 2];
        private bool inputEnabled, closing, requestedPatch;
        private bool speedBoost;
        private Process patchProcess;
        private int patchOffset;
        private bool loading, movementDisabled, fadeBlocked, fadeWriteDisabled;
        private bool available;
        private bool updateSuspended;
        internal bool InputDisabled => movementDisabled || loading || fadeBlocked || fadeWriteDisabled;
        internal event Action InputStateChanged;

        internal void SetAvailability(bool value) { available = value; UpdateState(); }
        internal bool CanAdjustSpeed => CanEdit && freezeCheckboxes.Any(box => box.Checked);
        private bool CanEdit => !updateSuspended && available && memory.ProcessHooked && !closing && !InputDisabled && !FadeMemory.SuspendPositionFreeze(memory.PositionX.Process, memory.Profile);
        internal event Action<bool> EditingChanged;

        public PositionControl(CrashMemory memory)
        {
            this.memory = memory;
            pointers = new[] { memory.PositionX, memory.PositionY, memory.PositionZ };
            Size = new Size(340, 112);
            for (int i = 0; i < editors.Length; i++)
            {
                int index = i;
                var label = new Label { Text = axes[i] + ":", AutoSize = true, Left = 12, Top = i * 26 + 4 };
                var editor = new TextBox { Left = 48, Top = i * 26, Width = 100, Text = "-", TextAlign = HorizontalAlignment.Right };
                var freeze = new CheckBox { Text = "Freeze " + axes[i], AutoSize = true, Left = 222, Top = i * 26 + 2 };
                freeze.CheckedChanged += (s, e) =>
                {
                    UpdateState();
                };
                freezeCheckboxes[i] = freeze;
                Controls.Add(label);
                Controls.Add(freeze);
                editor.KeyDown += (s, e) =>
                {
                    if (e.KeyCode != Keys.Enter) { EditingChanged?.Invoke(true); return; }
                    e.SuppressKeyPress = true;
                    if (!CanEdit) return;
                    float value;
                    if (!TryParseCoordinate(editor.Text, axes[index], out value))
                    {
                        MessageBox.Show(this, "Enter a finite number for " + axes[index] + ".", "Position", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    var update = new float?[3]; update[index] = value;
                    WritePosition(update);
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
                for (int side = 0; side < 2; side++)
                {
                    int buttonSide = side;
                    var button = HoldButton.Adjustment(i * 26, side == 0 ? -1 : 1,
                        "Position " + axes[i] + (side == 0 ? " minus" : " plus"), pressed =>
                        {
                            buttonHeld[index, buttonSide] = pressed;
                            PublishMotion();
                        });
                    movementButtons[i, side] = button;
                    Controls.Add(button);
                }
            }
            var save = new Button { Text = "Save", Left = 87, Top = 80, Width = 80 };
            teleportButton = new HoldButton { Text = "TP", Left = 172, Top = 80, Width = 80, Enabled = false, HeldAction = Teleport };
            save.Click += (s, e) => SavePosition();
            Controls.Add(save);
            Controls.Add(teleportButton);
            EnabledChanged += (s, e) => UpdateState();
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
            if (loading) return;
            editors[index].Text = available && memory.ProcessHooked ? FormatCoordinate(pointers[index].Read()) : "-";
        }

        public void RefreshValues()
        {
            UpdateState();
            for (int i = 0; i < editors.Length; i++) if (!editors[i].Focused) UpdateEditor(i);
        }

        // Thread-safe read for other workers. Returns null during loading, fades, or when unavailable.
        internal float[] ReadPlayerPosition()
        {
            if (closing) return null;
            try { return motion.ReadValues(); }
            catch (System.ComponentModel.Win32Exception) { return null; }
            catch (InvalidOperationException) { return null; }
        }

        internal void SavePosition()
        {
            if (!CanEdit) return;
            try
            {
                var current = motion.ReadValues();
                if (current == null) return;
                savedPosition = current;
                teleportButton.Enabled = CanEdit;
            }
            catch (Exception ex) { HelperLog.Error("Save player position", ex); }
        }

        internal void Teleport()
        {
            if (!CanEdit || savedPosition == null) return;
            WritePosition(savedPosition.Select(value => (float?)value).ToArray());
            RefreshValues();
        }
        internal void ToggleFreeze(int axis)
        {
            if (CanEdit) freezeCheckboxes[axis].Checked = !freezeCheckboxes[axis].Checked;
        }

        private void UpdateState()
        {
            bool connected = !updateSuspended && available && memory.ProcessHooked && !closing;
            bool writable = connected && !loading && !fadeBlocked && !fadeWriteDisabled && !memory.IsLoading;
            foreach (var editor in editors) if (editor != null) editor.Enabled = CanEdit;
            foreach (var checkbox in freezeCheckboxes) if (checkbox != null) checkbox.Enabled = writable;
            foreach (var button in Controls.OfType<Button>())
                if (!(button is HoldButton) || button == teleportButton)
                    button.Enabled = CanEdit && (button != teleportButton || savedPosition != null);
            for (int axis = 0; axis < 3; axis++)
                for (int side = 0; side < 2; side++)
                    movementButtons[axis, side].Enabled = CanEdit && (axis == 2 ? freezeCheckboxes[2].Checked : freezeCheckboxes[0].Checked || freezeCheckboxes[1].Checked);
            PublishMotion();
            UpdatePatch(connected ? memory.PositionX.Process : null, connected ? memory.Profile?.PositionCodeOffset ?? 0 : 0,
                writable && freezeCheckboxes.All(box => box != null && box.Checked));
        }

        private void PublishMotion()
        {
            if (closing) return;
            try
            {
                var directions = new int[3];
                for (int axis = 0; axis < directions.Length; axis++)
                    directions[axis] = Math.Sign((inputEnabled ? heldDirections[axis] : 0) + (buttonHeld[axis, 1] ? 1 : 0) - (buttonHeld[axis, 0] ? 1 : 0));
                motion.Configure(memory.PositionX.Process, memory.Profile, !updateSuspended && available && memory.ProcessHooked,
                    freezeCheckboxes.Select(box => box != null && box.Checked).ToArray(), !movementDisabled,
                    directions, (settings?.PositionXYZSpeed ?? 0.8f) * (speedBoost ? 2 : 1), viewOrientation);
            }
            catch (Exception ex) { HelperLog.Error("Configure player movement", ex); }
        }

        private void WritePosition(float?[] values)
        {
            if (!CanEdit) return;
            try { motion.Write(values); }
            catch (Exception ex) { HelperLog.Error("Write player position", ex); }
        }

        internal void ResetControls()
        {
            motion.ResetForLoading();
            movementDisabled = false;
            heldDirections = new int[3];
            speedBoost = false;
            foreach (var box in freezeCheckboxes) box.Checked = false;
            UpdateState(); InputStateChanged?.Invoke();
        }

        internal void SetFadeWriteDisabled(bool value)
        {
            fadeWriteDisabled = value;
            motion.SetFadeWriteDisabled(value);
            patchService.SetFadeWriteDisabled(value);
            if (value) foreach (var box in freezeCheckboxes) box.Checked = false;
            UpdateState(); InputStateChanged?.Invoke();
        }

        internal void SetLoading(bool value)
        {
            if (loading == value) return;
            loading = value;
            if (value) motion.ResetForLoading();
            if (value) foreach (var box in freezeCheckboxes) box.Checked = false;
            UpdateState(); InputStateChanged?.Invoke();
        }

        internal void SetFadeBlocked(bool value)
        {
            if (fadeBlocked == value) return;
            fadeBlocked = value; UpdateState(); InputStateChanged?.Invoke();
        }

        internal void ToggleMovement()
        {
            if (!available || loading || fadeBlocked || fadeWriteDisabled || memory.IsLoading) return;
            movementDisabled = !movementDisabled; UpdateState(); InputStateChanged?.Invoke();
        }

        private async void UpdatePatch(Process target, int offset, bool enabled)
        {
            if (closing || (ReferenceEquals(target, patchProcess) && offset == patchOffset && enabled == requestedPatch)) return;
            patchProcess = target; patchOffset = offset; requestedPatch = enabled;
            try { await patchService.ConfigureAsync(target, offset, enabled, memory.Profile); }
            catch (Exception ex) { HelperLog.Error("Configure player position instructions", ex); }
        }

        internal void ApplySettings(HelperSettings settings, Func<float[]> orientation)
        {
            this.settings = settings;
            viewOrientation = orientation;
            PublishMotion();
        }

        internal void SetMovement(int[] directions)
        {
            heldDirections = (int[])directions.Clone(); PublishMotion();
        }

        internal void SetSpeedBoost(bool enabled) { speedBoost = enabled; PublishMotion(); }

        internal void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled) { heldDirections = new int[3]; speedBoost = false; }
            PublishMotion();
        }

        internal async Task ShutdownAsync()
        {
            closing = true; inputEnabled = false;
            await motion.ShutdownAsync();
            await patchService.ConfigureAsync(null, 0, false);
        }

        internal Task SuspendForUpdateAsync()
        {
            updateSuspended = true;
            PublishMotion();
            patchProcess = null; patchOffset = 0; requestedPatch = false;
            return patchService.ConfigureAsync(null, 0, false);
        }

        internal void ResumeAfterUpdate() { updateSuspended = false; UpdateState(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { closing = true; motion.Dispose(); patchService.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
