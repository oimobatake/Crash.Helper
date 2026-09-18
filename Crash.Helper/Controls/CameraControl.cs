using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Memory.Camera;

namespace Crash.Helper.Controls
{
    internal sealed class CameraControl : UserControl
    {
        private readonly CameraService service = new CameraService();
        private readonly TextBox[] editors = new TextBox[5];
        private readonly string[] axes = { "X", "Y", "Z", "Yaw", "Pitch" };
        private readonly CheckBox freezeXYZ, freezeYawPitch;
        private readonly TextBox xyzSpeedEditor, yawPitchSpeedEditor;
        private readonly Button saveButton, teleportButton;
        private readonly ToolTip toolTip = new ToolTip();
        private readonly Timer refreshTimer = new Timer { Interval = 33 };
        private Process process;
        private CameraMemoryProfile profile;
        private float[] values, saved;
        private float xyzSpeed = 30f, yawPitchSpeed = 0.03f;
        private int[] heldDirections = new int[5];
        private bool available, ready, changing, refreshing, suppressChanges, closing;
        private int generation;

        internal event Action<bool> EditingChanged;

        internal CameraControl()
        {
            Size = new Size(270, 224);
            for (int i = 0; i < editors.Length; i++)
            {
                int axis = i;
                var label = new Label { Text = axes[i] + ":", AutoSize = true, Left = 12, Top = i * 26 + 4 };
                var editor = new TextBox { Left = 48, Top = i * 26, Width = 100, Text = "-", TextAlign = HorizontalAlignment.Right };
                editor.Enter += (s, e) => EditingChanged?.Invoke(true);
                editor.MouseDown += (s, e) => EditingChanged?.Invoke(true);
                editor.Leave += (s, e) => { editor.Select(0, 0); UpdateEditor(axis); EditingChanged?.Invoke(false); };
                editor.KeyDown += async (s, e) =>
                {
                    if (e.KeyCode != Keys.Enter) { EditingChanged?.Invoke(true); return; }
                    e.SuppressKeyPress = true;
                    if (!CanWrite(axis)) return;
                    float value;
                    if (!float.TryParse(editor.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || float.IsInfinity(value))
                    {
                        HelperLog.Error("Edit camera", new ArgumentException("Enter a finite number for " + axes[axis] + "."));
                        EndEditing();
                        return;
                    }
                    var update = new float?[5];
                    update[axis] = value;
                    EndEditing();
                    await WriteAsync(update);
                };
                editors[i] = editor;
                Controls.Add(label);
                Controls.Add(editor);
            }
            freezeXYZ = new CheckBox { Text = "Freeze XYZ", AutoSize = true, Left = 160, Top = 28 };
            freezeYawPitch = new CheckBox { Text = "Freeze YawPitch", AutoSize = true, Left = 160, Top = 93 };
            freezeXYZ.CheckedChanged += OnFreezeChanged;
            freezeYawPitch.CheckedChanged += OnFreezeChanged;
            Controls.Add(freezeXYZ);
            Controls.Add(freezeYawPitch);
            xyzSpeedEditor = CreateSpeedEditor("XYZ Speed:", 138, () => xyzSpeed, value => xyzSpeed = value);
            yawPitchSpeedEditor = CreateSpeedEditor("YawPitch Speed:", 164, () => yawPitchSpeed, value => yawPitchSpeed = value);
            saveButton = new Button { Text = "Save", Left = 52, Top = 196, Width = 80 };
            teleportButton = new Button { Text = "TP", Left = 137, Top = 196, Width = 80 };
            saveButton.Click += (s, e) => SaveCamera();
            teleportButton.Click += (s, e) => Teleport();
            toolTip.SetToolTip(teleportButton, "Restore saved camera values for the enabled Freeze groups.");
            Controls.Add(saveButton);
            Controls.Add(teleportButton);
            MouseDown += (s, e) => EndEditing();
            foreach (Control control in Controls)
                if (control is Label) control.MouseDown += (s, e) => EndEditing();
            refreshTimer.Tick += async (s, e) => await RefreshAsync();
            UpdateState();
        }

        private TextBox CreateSpeedEditor(string title, int top, Func<float> get, Action<float> set)
        {
            Controls.Add(new Label { Text = title, AutoSize = true, Left = 12, Top = top + 4 });
            var editor = new TextBox { Left = 120, Top = top, Width = 80, Text = get().ToString("R", CultureInfo.InvariantCulture), TextAlign = HorizontalAlignment.Right };
            editor.Enter += (s, e) => EditingChanged?.Invoke(true);
            editor.MouseDown += (s, e) => EditingChanged?.Invoke(true);
            editor.Leave += (s, e) =>
            {
                editor.Text = get().ToString("R", CultureInfo.InvariantCulture);
                editor.Select(0, 0);
                EditingChanged?.Invoke(false);
            };
            editor.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) { EditingChanged?.Invoke(true); return; }
                e.SuppressKeyPress = true;
                float value;
                if (float.TryParse(editor.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= 1000)
                    set(value);
                else HelperLog.Error("Edit camera speed", new ArgumentException("Speed must be a number between 0 and 1000."));
                EndEditing();
                UpdateMovement();
            };
            toolTip.SetToolTip(editor, "Press Enter to apply. Leaving this field discards changes.");
            Controls.Add(editor);
            return editor;
        }

        internal void EndEditing()
        {
            if (IsDisposed || Disposing) return;
            for (int i = 0; i < editors.Length; i++)
            {
                editors[i].Select(0, 0);
                UpdateEditor(i);
            }
            xyzSpeedEditor.Text = xyzSpeed.ToString("R", CultureInfo.InvariantCulture);
            yawPitchSpeedEditor.Text = yawPitchSpeed.ToString("R", CultureInfo.InvariantCulture);
            xyzSpeedEditor.Select(0, 0);
            yawPitchSpeedEditor.Select(0, 0);
            ActiveControl = null;
            if (FindForm() != null) FindForm().ActiveControl = null;
            EditingChanged?.Invoke(false);
        }

        internal void SetAvailability(Process target, CameraMemoryProfile targetProfile)
        {
            if (closing) return;
            if (ReferenceEquals(process, target) && ReferenceEquals(profile, targetProfile)) return;
            process = target;
            profile = targetProfile;
            available = target != null && targetProfile != null;
            ready = false;
            values = saved = null;
            heldDirections = new int[5];
            suppressChanges = true;
            freezeXYZ.Checked = freezeYawPitch.Checked = false;
            suppressChanges = false;
            Configure();
        }

        private void OnFreezeChanged(object sender, EventArgs e)
        {
            if (!suppressChanges) Configure();
        }

        private async void Configure()
        {
            int version = ++generation;
            changing = true;
            refreshTimer.Stop();
            UpdateState();
            try
            {
                await service.ConfigureAsync(available ? process : null, available ? profile : null, freezeXYZ.Checked, freezeYawPitch.Checked);
                if (version != generation || IsDisposed) return;
                ready = available;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                HelperLog.Error("Configure camera", ex);
                if (version == generation && !IsDisposed)
                {
                    ready = false;
                    suppressChanges = true;
                    freezeXYZ.Checked = freezeYawPitch.Checked = false;
                    suppressChanges = false;
                }
            }
            finally
            {
                if (version == generation && !IsDisposed)
                {
                    changing = false;
                    UpdateState();
                    if (ready && !closing) refreshTimer.Start();
                }
            }
        }

        private bool CanWrite(int axis) => available && ready && !changing && !closing && values != null &&
            (axis < 3 ? freezeXYZ.Checked : freezeYawPitch.Checked);

        private void UpdateState()
        {
            for (int i = 0; i < editors.Length; i++)
            {
                editors[i].Enabled = CanWrite(i);
                if (!editors[i].Focused) UpdateEditor(i);
            }
            freezeXYZ.Enabled = freezeYawPitch.Enabled = available && ready && !changing && !closing;
            xyzSpeedEditor.Enabled = yawPitchSpeedEditor.Enabled = available && ready && !closing;
            saveButton.Enabled = available && ready && !changing && values != null && !closing;
            teleportButton.Enabled = saved != null && (CanWrite(0) || CanWrite(3));
            UpdateMovement();
        }

        private void UpdateEditor(int axis)
        {
            editors[axis].Text = values == null ? "-" : ((double)values[axis]).ToString("F8", CultureInfo.InvariantCulture);
        }

        private async Task RefreshAsync()
        {
            if (!ready || changing || refreshing || closing) return;
            int version = generation;
            refreshing = true;
            try
            {
                var current = await service.ReadAsync();
                if (version != generation || IsDisposed) return;
                values = current;
                UpdateState();
            }
            catch (Exception ex) { HelperLog.Error("Read camera", ex); }
            finally { refreshing = false; }
        }

        private async Task WriteAsync(float?[] update)
        {
            int version = generation;
            try
            {
                await service.WriteAsync(update);
                if (version == generation && !IsDisposed) await RefreshAsync();
            }
            catch (Exception ex) { HelperLog.Error("Write camera", ex); }
        }

        internal void ToggleFreeze(bool rotation)
        {
            var checkbox = rotation ? freezeYawPitch : freezeXYZ;
            // Shared hotkeys may toggle both freeze groups before the first async update completes.
            if (available && ready && !closing) checkbox.Checked = !checkbox.Checked;
        }

        internal async void SaveCamera()
        {
            if (!saveButton.Enabled) return;
            int version = generation;
            try
            {
                var current = await service.ReadAsync();
                if (version != generation || IsDisposed || current == null) return;
                saved = current;
                teleportButton.Enabled = CanWrite(0) || CanWrite(3);
            }
            catch (Exception ex) { HelperLog.Error("Save camera", ex); }
        }

        internal async void Teleport()
        {
            if (!teleportButton.Enabled) return;
            var update = new float?[5];
            for (int i = 0; i < 5; i++) if (CanWrite(i)) update[i] = saved[i];
            await WriteAsync(update);
        }

        internal void SetMovement(int[] directions)
        {
            heldDirections = (int[])directions.Clone();
            UpdateMovement();
        }

        private void UpdateMovement()
        {
            var directions = new int[5];
            if (available && ready && !changing && !closing)
                for (int i = 0; i < directions.Length; i++)
                    if (i < 3 ? freezeXYZ.Checked : freezeYawPitch.Checked) directions[i] = heldDirections[i];
            service.SetMovement(directions, xyzSpeed, yawPitchSpeed);
        }

        internal async Task ShutdownAsync()
        {
            closing = true;
            generation++;
            refreshTimer.Stop();
            UpdateState();
            await service.ShutdownAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                closing = true;
                generation++;
                refreshTimer.Dispose();
                toolTip.Dispose();
                // Normal window closing awaits cleanup first. Direct disposal also restores
                // patches synchronously, rather than abandoning a background cleanup at exit.
                try { service.Dispose(); }
                catch (Exception ex) { HelperLog.Error("Dispose camera", ex); throw; }
            }
            base.Dispose(disposing);
        }
    }
}
