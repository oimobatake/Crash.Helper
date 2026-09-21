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
            Size = new Size(270, 112);
            for (int i = 0; i < editors.Length; i++)
            {
                int index = i;
                var label = new Label { Text = axes[i] + ":", AutoSize = true, Left = 12, Top = i * 26 + 4 };
                var editor = new TextBox { Left = 48, Top = i * 26, Width = 100, Text = "-", TextAlign = HorizontalAlignment.Right };
                var freeze = new CheckBox { Text = "Freeze " + axes[i], AutoSize = true, Left = 160, Top = i * 26 + 2 };
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
            }
            var save = new Button { Text = "Save", Left = 52, Top = 80, Width = 80 };
            teleportButton = new Button { Text = "TP", Left = 137, Top = 80, Width = 80, Enabled = false };
            save.Click += (s, e) => SavePosition();
            teleportButton.Click += (s, e) => Teleport();
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
            foreach (var button in Controls.OfType<Button>()) button.Enabled = CanEdit && (button != teleportButton || savedPosition != null);
            PublishMotion();
            UpdatePatch(connected ? memory.PositionX.Process : null, connected ? memory.Profile?.PositionCodeOffset ?? 0 : 0,
                writable && freezeCheckboxes.All(box => box != null && box.Checked));
        }

        private void PublishMotion()
        {
            if (closing) return;
            try
            {
                motion.Configure(memory.PositionX.Process, memory.Profile, !updateSuspended && available && memory.ProcessHooked,
                    freezeCheckboxes.Select(box => box != null && box.Checked).ToArray(), inputEnabled && !movementDisabled,
                    heldDirections, (settings?.PositionXYZSpeed ?? 0.8f) * (speedBoost ? 2 : 1), viewOrientation);
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
