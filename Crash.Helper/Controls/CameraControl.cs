using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Memory.Camera;
using Crash.Helper.Input;

namespace Crash.Helper.Controls
{
    internal sealed class CameraControl : UserControl
    {
        private readonly CameraService service = new CameraService();
        private readonly TextBox[] editors = new TextBox[5];
        private readonly string[] axes = { "X", "Y", "Z", "Yaw", "Pitch" };
        private readonly CheckBox freezeXYZ, freezeYawPitch, followPlayer, orbitPlayer;
        // Orbit H, Orbit V, and Zoom (distance from the player in player units).
        private readonly TextBox[] orbitEditors = new TextBox[3];
        private const double OrbitStepDistance = 1;
        private double orbitHorizontalStep = 5, orbitVerticalStep = 5;
        private readonly MouseCameraListener mouse = new MouseCameraListener();
        private readonly Button saveButton, teleportButton;
        private readonly ToolTip toolTip = new ToolTip();
        private readonly Timer refreshTimer = new Timer { Interval = 33 };
        private Process process;
        private CameraMemoryProfile profile;
        private float[] values, saved;
        private float xyzSpeed = 30.0f, yawPitchSpeed = 0.03f;
        private float mouseXSensitivity = 0.001f, mouseYSensitivity = 0.001f;
        private float followScale = HelperSettings.DefaultCameraFollowScale;
        private float followDeadzone = HelperSettings.DefaultCameraFollowVerticalDeadzone;
        private float followSpeed = HelperSettings.DefaultCameraFollowSpeed;
        private bool followPitch = true, mouseControl, invertMouseX, invertMouseY, inputEnabled;
        private bool speedBoost, loading, movementDisabled;
        internal Func<bool> IsLoading { get; set; } = () => false;
        // Called on the camera worker. Returns null while the player position cannot be read.
        internal Func<float[]> PlayerPosition { get; set; }
        internal bool CanAdjustXYZSpeed => CanWrite(0);
        internal bool CanAdjustRotationSpeed => CanWrite(3);
        internal bool CanAdjustMouseSensitivity => CanWrite(3);
        private HelperSettings settings;
        private int[] heldDirections = new int[5];
        private bool available, ready, changing, refreshing, suppressChanges, closing;
        private int generation;
        private bool updateSuspended;

        internal event Action<bool> EditingChanged;

        internal CameraControl()
        {
            Size = new Size(270, 250);
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
            freezeXYZ = new CheckBox { Text = "Control XYZ", AutoSize = true, Left = 160, Top = 28 };
            freezeYawPitch = new CheckBox { Text = "Control YawPitch", AutoSize = true, Left = 160, Top = 93 };
            followPlayer = new CheckBox { Text = "Follow player", AutoSize = true, Left = 160, Top = 172 };
            freezeXYZ.CheckedChanged += OnFreezeChanged;
            freezeYawPitch.CheckedChanged += OnFreezeChanged;
            followPlayer.CheckedChanged += OnFollowChanged;
            Controls.Add(freezeXYZ);
            Controls.Add(freezeYawPitch);
            Controls.Add(followPlayer);
            mouse.Moved += OnMouseMovement;
            mouse.Wheel += OnMouseWheel;
            saveButton = new Button { Text = "Save", Left = 52, Top = 138, Width = 80 };
            teleportButton = new Button { Text = "TP", Left = 137, Top = 138, Width = 80 };
            saveButton.Click += (s, e) => SaveCamera();
            teleportButton.Click += (s, e) => Teleport();
            toolTip.SetToolTip(teleportButton, "Restore saved camera values for the enabled Control groups.");
            Controls.Add(saveButton);
            Controls.Add(teleportButton);
            string[] orbitAxes = { "Orbit H", "Orbit V", "Zoom" };
            for (int i = 0; i < orbitEditors.Length; i++)
            {
                int axis = i;
                var label = new Label { Text = orbitAxes[i] + ":", AutoSize = true, Left = 12, Top = 174 + i * 26 };
                var editor = new TextBox { Left = 68, Top = 170 + i * 26, Width = 80, Text = "-", TextAlign = HorizontalAlignment.Right };
                editor.Enter += (s, e) => EditingChanged?.Invoke(true);
                editor.MouseDown += (s, e) => EditingChanged?.Invoke(true);
                editor.Leave += (s, e) => { editor.Select(0, 0); UpdateOrbitEditor(axis); EditingChanged?.Invoke(false); };
                editor.KeyDown += (s, e) =>
                {
                    if (e.KeyCode != Keys.Enter) { EditingChanged?.Invoke(true); return; }
                    e.SuppressKeyPress = true;
                    if (!OrbitActive) return;
                    double value;
                    if (!double.TryParse(editor.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value))
                    {
                        HelperLog.Error("Edit camera orbit", new ArgumentException("Enter a finite number for " + orbitAxes[axis] + "."));
                        EndEditing();
                        return;
                    }
                    if (axis == 2)
                    {
                        if (value > 0) service.RequestOrbitDistance(value);
                        EndEditing();
                        return;
                    }
                    value %= 360;
                    if (value < 0) value += 360;
                    service.RequestOrbitAngles(axis == 0 ? value : double.NaN, axis == 1 ? value : double.NaN);
                    EndEditing();
                };
                orbitEditors[i] = editor;
                Controls.Add(label);
                Controls.Add(editor);
            }
            orbitPlayer = new CheckBox { Text = "Orbit player", AutoSize = true, Left = 160, Top = 198 };
            orbitPlayer.CheckedChanged += OnOrbitChanged;
            Controls.Add(orbitPlayer);
            MouseDown += (s, e) => EndEditing();
            foreach (Control control in Controls)
                if (control is Label) control.MouseDown += (s, e) => EndEditing();
            refreshTimer.Tick += async (s, e) => await RefreshAsync();
            UpdateState();
        }

        internal void EndEditing()
        {
            if (IsDisposed || Disposing) return;
            for (int i = 0; i < editors.Length; i++)
            {
                editors[i].Select(0, 0);
                UpdateEditor(i);
            }
            for (int i = 0; i < orbitEditors.Length; i++)
            {
                orbitEditors[i].Select(0, 0);
                UpdateOrbitEditor(i);
            }
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
            values = null;
            heldDirections = new int[5];
            suppressChanges = true;
            freezeXYZ.Checked = freezeYawPitch.Checked = followPlayer.Checked = orbitPlayer.Checked = false;
            suppressChanges = false;
            Configure();
        }

        private void OnFreezeChanged(object sender, EventArgs e)
        {
            if (suppressChanges) return;
            suppressChanges = true;
            if (!freezeXYZ.Checked) followPlayer.Checked = false;
            if (!freezeXYZ.Checked || !freezeYawPitch.Checked) orbitPlayer.Checked = false;
            suppressChanges = false;
            Configure();
        }

        private void OnFollowChanged(object sender, EventArgs e)
        {
            if (suppressChanges) return;
            // Following writes the camera XYZ, which requires the game's position writer to be removed.
            if (!followPlayer.Checked && orbitPlayer.Checked) orbitPlayer.Checked = false;
            if (followPlayer.Checked && !freezeXYZ.Checked) freezeXYZ.Checked = true;
            else UpdateMovement();
        }

        private void OnOrbitChanged(object sender, EventArgs e)
        {
            if (suppressChanges) return;
            if (!orbitPlayer.Checked) { UpdateMovement(); return; }
            // Orbiting writes both the position and the view direction.
            suppressChanges = true;
            bool reconfigure = !freezeXYZ.Checked || !freezeYawPitch.Checked;
            followPlayer.Checked = freezeXYZ.Checked = freezeYawPitch.Checked = true;
            suppressChanges = false;
            if (reconfigure) Configure();
            else UpdateMovement();
        }

        private bool OrbitActive => FollowActive && freezeYawPitch.Checked && orbitPlayer.Checked;

        private bool FollowActive => !updateSuspended && available && ready && !changing && !closing && !loading && freezeXYZ.Checked && followPlayer.Checked;

        private void UpdateOrbitEditor(int axis)
        {
            if (axis == 2)
            {
                float distance = OrbitActive ? service.OrbitDistance : -1;
                orbitEditors[axis].Text = distance < 0 ? "-" : distance.ToString("F2", CultureInfo.InvariantCulture);
                return;
            }
            var angles = OrbitActive ? service.OrbitDegrees : null;
            orbitEditors[axis].Text = angles == null ? "-" : angles[axis].ToString("F1", CultureInfo.InvariantCulture);
        }

        private async void Configure()
        {
            if (updateSuspended) return;
            int version = ++generation;
            changing = true;
            refreshTimer.Stop();
            UpdateState();
            try
            {
                await service.ConfigureAsync(available ? process : null, available ? profile : null, freezeXYZ.Checked && !loading, freezeYawPitch.Checked && !loading);
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
                    if (!loading) freezeXYZ.Checked = freezeYawPitch.Checked = false;
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

        private bool CanWrite(int axis) => !updateSuspended && available && ready && !changing && !closing && !loading && !IsLoading() && !movementDisabled && values != null &&
            (axis < 3 ? freezeXYZ.Checked : freezeYawPitch.Checked);

        private void UpdateState()
        {
            for (int i = 0; i < editors.Length; i++)
            {
                editors[i].Enabled = CanWrite(i);
                if (!editors[i].Focused) UpdateEditor(i);
            }
            freezeXYZ.Enabled = freezeYawPitch.Enabled = followPlayer.Enabled = orbitPlayer.Enabled = available && ready && !changing && !closing && !loading;
            for (int i = 0; i < orbitEditors.Length; i++)
            {
                orbitEditors[i].Enabled = OrbitActive;
                if (!orbitEditors[i].Focused) UpdateOrbitEditor(i);
            }
            saveButton.Enabled = available && ready && !changing && values != null && !closing && !loading && !movementDisabled;
            teleportButton.Enabled = saved != null && (CanWrite(0) || CanWrite(3));
            UpdateMovement();
        }

        private void UpdateEditor(int axis)
        {
            editors[axis].Text = values == null ? "-" : ((double)values[axis]).ToString("F8", CultureInfo.InvariantCulture);
        }

        private async Task RefreshAsync()
        {
            if (!ready || changing || refreshing || closing || loading) return;
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

        internal void ToggleFollow()
        {
            if (CanToggleControls) followPlayer.Checked = !followPlayer.Checked;
        }

        // Axis 0 = Orbit H, 1 = Orbit V, 2 = Zoom.
        internal void StepOrbit(int axis, int direction)
        {
            if (!CanToggleControls || !OrbitActive) return;
            if (axis == 2) service.StepOrbitDistance(direction * OrbitStepDistance);
            else service.StepOrbitAngles(axis == 0 ? direction * orbitHorizontalStep : 0, axis == 1 ? direction * orbitVerticalStep : 0);
        }

        internal void ToggleOrbit()
        {
            if (CanToggleControls) orbitPlayer.Checked = !orbitPlayer.Checked;
        }

        private bool CanToggleControls => !updateSuspended && available && ready && !closing && !loading && !movementDisabled && !IsLoading();

        internal ISet<Hotkey> HandleCombinedHotkeys(Hotkey[] actions)
        {
            var handled = new HashSet<Hotkey>();
            var xyz = actions.FirstOrDefault(action => action.Label == "Control XYZ");
            var rotation = actions.FirstOrDefault(action => action.Label == "Control YawPitch");
            var follow = actions.FirstOrDefault(action => action.Label == "Follow Player");
            var orbit = actions.FirstOrDefault(action => action.Label == "Orbit Player");
            bool sharedOrbit = orbit != null && (xyz != null || rotation != null);
            bool sharedFollow = follow != null && xyz != null;
            if (!sharedOrbit && !sharedFollow) return handled;
            if (sharedOrbit) { handled.Add(orbit); if (xyz != null) handled.Add(xyz); if (rotation != null) handled.Add(rotation); }
            if (sharedOrbit && follow != null) handled.Add(follow);
            if (sharedFollow) { handled.Add(follow); handled.Add(xyz); }
            if (!CanToggleControls) return handled;
            bool enabled = sharedOrbit ? !orbitPlayer.Checked : !followPlayer.Checked;
            bool reconfigure = false;
            suppressChanges = true;
            if (xyz != null && handled.Contains(xyz)) { reconfigure |= freezeXYZ.Checked != enabled; freezeXYZ.Checked = enabled; }
            if (rotation != null && handled.Contains(rotation)) { reconfigure |= freezeYawPitch.Checked != enabled; freezeYawPitch.Checked = enabled; }
            if (follow != null && handled.Contains(follow)) followPlayer.Checked = enabled;
            if (sharedOrbit)
            {
                orbitPlayer.Checked = enabled;
                if (enabled)
                {
                    reconfigure |= !freezeXYZ.Checked || !freezeYawPitch.Checked;
                    followPlayer.Checked = freezeXYZ.Checked = freezeYawPitch.Checked = true;
                }
            }
            if (!freezeXYZ.Checked) followPlayer.Checked = false;
            if (!freezeXYZ.Checked || !freezeYawPitch.Checked || !followPlayer.Checked) orbitPlayer.Checked = false;
            suppressChanges = false;
            if (reconfigure) Configure(); else UpdateState();
            return handled;
        }

        internal void ToggleFreeze(bool rotation)
        {
            var checkbox = rotation ? freezeYawPitch : freezeXYZ;
            // Shared hotkeys may toggle both freeze groups before the first async update completes.
            if (available && ready && !closing && !loading && !movementDisabled && !IsLoading()) checkbox.Checked = !checkbox.Checked;
        }

        internal async void SaveCamera()
        {
            if (!available || !ready || changing || values == null || closing || loading || movementDisabled || IsLoading()) return;
            int version = generation;
            try
            {
                var current = await service.ReadAsync();
                if (version != generation || IsDisposed || current == null || loading || IsLoading()) return;
                saved = current;
                teleportButton.Enabled = CanWrite(0) || CanWrite(3);
            }
            catch (Exception ex) { HelperLog.Error("Save camera", ex); }
        }

        internal async void Teleport()
        {
            if (saved == null || (!CanWrite(0) && !CanWrite(3))) return;
            var update = new float?[5];
            for (int i = 0; i < 5; i++) if (CanWrite(i)) update[i] = saved[i];
            await WriteAsync(update);
        }

        internal void SetMovement(int[] directions)
        {
            heldDirections = (int[])directions.Clone();
            UpdateMovement();
        }

        internal float[] GetViewOrientation()
        {
            var current = values;
            return available && ready && !changing && !closing && !loading && current != null ? new[] { current[3], current[4] } : null;
        }

        internal void ResetControls()
        {
            movementDisabled = false;
            heldDirections = new int[5];
            speedBoost = false;
            suppressChanges = true;
            freezeXYZ.Checked = freezeYawPitch.Checked = followPlayer.Checked = orbitPlayer.Checked = false;
            suppressChanges = false;
            Configure(); InputStateChanged?.Invoke();
        }

        internal void SetLoading(bool value)
        {
            if (loading == value || closing) return;
            loading = value;
            suppressChanges = true;
            if (value) freezeXYZ.Checked = freezeYawPitch.Checked = false;
            // Following persists across levels, so restore its required XYZ control after loading.
            else
            {
                if (followPlayer.Checked) freezeXYZ.Checked = true;
                if (orbitPlayer.Checked) freezeYawPitch.Checked = true;
            }
            suppressChanges = false;
            Configure(); InputStateChanged?.Invoke();
        }

        internal bool InputDisabled => movementDisabled || loading;
        internal event Action InputStateChanged;

        internal void ToggleMovement()
        {
            if (!available || loading || IsLoading()) return;
            movementDisabled = !movementDisabled; UpdateState(); InputStateChanged?.Invoke();
        }

        private void UpdateMovement()
        {
            var directions = new int[5];
            if (!updateSuspended && inputEnabled && available && ready && !changing && !closing && !loading && !movementDisabled)
                for (int i = 0; i < directions.Length; i++)
                    if (i < 3 ? freezeXYZ.Checked : freezeYawPitch.Checked) directions[i] = heldDirections[i];
            service.SetFollow(FollowActive ? PlayerPosition : null, followScale, followDeadzone, followSpeed);
            service.SetOrbit(OrbitActive);
            bool useMouse = inputEnabled && mouseControl && CanWrite(3);
            try
            {
                // Orbit zoom uses the wheel even when mouse look is off.
                if (useMouse || (inputEnabled && !movementDisabled && OrbitActive)) mouse.Start(process.Id);
                else mouse.Stop();
            }
            catch (Exception ex) { useMouse = false; HelperLog.Error("Configure mouse input", ex); }
            service.SetMovement(directions, xyzSpeed * (speedBoost ? 2 : 1), yawPitchSpeed, followPitch, useMouse && mouse.IsTargetForeground);
        }

        internal void ApplySettings(HelperSettings settings, IReadOnlyList<Hotkey> hotkeys)
        {
            this.settings = settings;
            followPitch = settings.CameraMoveWithPitch;
            mouseControl = settings.CameraMouseControl;
            invertMouseX = settings.CameraInvertMouseX;
            invertMouseY = settings.CameraInvertMouseY;
            xyzSpeed = settings.CameraXYZSpeed;
            yawPitchSpeed = settings.CameraYawPitchSpeed;
            mouseXSensitivity = settings.CameraMouseXSensitivity;
            mouseYSensitivity = settings.CameraMouseYSensitivity;
            orbitHorizontalStep = settings.CameraOrbitHorizontalStep;
            orbitVerticalStep = settings.CameraOrbitVerticalStep;
            followScale = settings.CameraFollowScale;
            followDeadzone = settings.CameraFollowVerticalDeadzone;
            followSpeed = settings.CameraFollowSpeed;
            UpdateMovement();
        }

        internal void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            UpdateMovement();
        }

        internal void SetSpeedBoost(bool enabled)
        {
            speedBoost = enabled;
            UpdateMovement();
        }

        private void OnMouseWheel(double notches)
        {
            // Wheel forward zooms in, matching common orbit cameras.
            if (!inputEnabled || movementDisabled || !OrbitActive || IsLoading() || !mouse.IsTargetForeground) return;
            service.StepOrbitDistance(-notches * OrbitStepDistance);
        }

        private void OnMouseMovement(int x, int y)
        {
            if (!inputEnabled || !mouseControl || !CanWrite(3) || !mouse.IsTargetForeground) return;
            UpdateMovement();
            service.AddMouseMovement(-x * (double)mouseXSensitivity * (invertMouseX ? -1 : 1), y * (double)mouseYSensitivity * (invertMouseY ? -1 : 1));
        }

        internal async Task ShutdownAsync()
        {
            closing = true;
            generation++;
            refreshTimer.Stop();
            UpdateState();
            await service.ShutdownAsync();
        }

        internal Task SuspendForUpdateAsync()
        {
            updateSuspended = true;
            ++generation;
            refreshTimer.Stop();
            UpdateMovement();
            return service.ConfigureAsync(null, null, false, false);
        }

        internal void ResumeAfterUpdate() { updateSuspended = false; Configure(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                closing = true;
                generation++;
                refreshTimer.Dispose();
                toolTip.Dispose();
                mouse.Dispose();
                // Normal window closing awaits cleanup first. Direct disposal also restores
                // patches synchronously, rather than abandoning a background cleanup at exit.
                try { service.Dispose(); }
                catch (Exception ex) { HelperLog.Error("Dispose camera", ex); throw; }
            }
            base.Dispose(disposing);
        }
    }
}
