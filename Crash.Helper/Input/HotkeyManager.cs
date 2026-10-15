using System;
using System.Collections.Generic;
using System.Linq;

namespace Crash.Helper.Input
{
    internal sealed class HotkeyManager : IDisposable
    {
        private readonly HelperSettings settings;
        private readonly Func<bool> gameAvailable;
        private readonly Func<bool> foregroundAllowed;
        private readonly System.Windows.Forms.Timer focusTimer = new System.Windows.Forms.Timer { Interval = 33 };
        private bool lastForegroundAllowed;
        private readonly Action<Action> dispatch;
        private readonly KeyboardHotkeyListener listener = new KeyboardHotkeyListener();
        private readonly System.Windows.Forms.Timer repeatTimer = new System.Windows.Forms.Timer { Interval = 8 };
        private readonly HashSet<Hotkey> heldActions = new HashSet<Hotkey>();
        private readonly XInputListener gamepad = new XInputListener(8);
        private readonly PlayStationListener playStation;
        // Button masks per controller, written by the XInput poll thread and the UI thread.
        private readonly Dictionary<object, uint> padButtons = new Dictionary<object, uint>();
        private uint gamepadButtons;
        // Raised on the UI thread with the buttons held across all controllers, also while editing.
        public event Action<uint> GamepadButtonsChanged;
        private int[] movement = new int[5];
        private int[] positionMovement = new int[3];
        public event Action<int[]> PositionMovementChanged;
        public event Action<int[]> CameraMovementChanged;
        public event Action<bool> CameraSpeedBoostChanged;
        public event Action<bool> PositionSpeedBoostChanged;
        private bool positionSpeedBoost;
        private bool speedBoost;
        private bool ready;
        private bool editing;
        private bool disposed;
        private int generation;
        public IReadOnlyList<Hotkey> Hotkeys { get; }
        public string Status { get; private set; }
        public bool IsActive { get; private set; }
        internal bool CanUseCameraInput => !disposed && ready && !editing && Enabled && gameAvailable() && foregroundAllowed();
        internal Func<bool> AdvancedInputBlocked { get; set; } = () => false;
        private bool Allows(Hotkey action)
        {
            if ((action.Group != "Position" && action.Group != "Camera") || !AdvancedInputBlocked()) return true;
            // Saving, teleporting and toggling input remain available in the pause menu.
            switch (action.Label)
            {
                case "Position Save":
                case "Position TP":
                case "Camera Save":
                case "Camera TP":
                case "Toggle position input":
                case "Toggle camera input":
                    return true;
                default:
                    return false;
            }
        }
        public event EventHandler StatusChanged;
        public bool Enabled
        {
            get => settings.HotkeysEnabled;
            set { settings.HotkeysEnabled = value; UpdateState(); }
        }

        public HotkeyManager(IReadOnlyList<Hotkey> hotkeys, HelperSettings settings, Func<bool> gameAvailable, Action<Action> dispatch, Func<bool> foregroundAllowed = null)
        {
            Hotkeys = hotkeys;
            this.settings = settings;
            this.gameAvailable = gameAvailable;
            this.dispatch = dispatch;
            this.foregroundAllowed = foregroundAllowed ?? (() => true);
            var currentNames = new HashSet<string>(hotkeys.Select(h => h.Label));
            foreach (var name in settings.Hotkeys.Keys.Where(name => !currentNames.Contains(name)).ToArray()) settings.Hotkeys.Remove(name);
            foreach (var hotkey in hotkeys)
            {
                HotkeyBinding binding;
                if (!settings.Hotkeys.TryGetValue(hotkey.Label, out binding) || binding == null)
                    settings.Hotkeys[hotkey.Label] = binding = new HotkeyBinding();
                hotkey.Key = binding.Key;
                hotkey.Modifier = binding.Modifiers;
                hotkey.GamepadMask = binding.Gamepad;
            }
            gamepad.StateChanged += (index, buttons) => { SetPadButtons(index, buttons); dispatch(UpdateGamepad); };
            try
            {
                playStation = new PlayStationListener();
                playStation.StateChanged += (device, buttons) => { SetPadButtons(device, buttons); UpdateGamepad(); };
            }
            catch (Exception ex) { HelperLog.Error("Listen for PlayStation controllers", ex); }
            listener.KeyPressed += OnKeyPressed;
            listener.KeyReleased += OnKeyReleased;
            repeatTimer.Tick += (s, e) => RepeatHeldActions();
            focusTimer.Tick += (s, e) => { if (lastForegroundAllowed != this.foregroundAllowed()) UpdateState(); };
            UpdateState();
        }

        public void ReloadSettings()
        {
            foreach (var hotkey in Hotkeys)
            {
                HotkeyBinding binding;
                if (settings.Hotkeys.TryGetValue(hotkey.Label, out binding))
                {
                    hotkey.Key = binding.Key;
                    hotkey.Modifier = binding.Modifiers;
                    hotkey.GamepadMask = binding.Gamepad;
                }
            }
            UpdateState();
        }

        public bool TrySetBinding(int index, uint key, KeyModifiers modifiers)
        {
            var hotkey = Hotkeys[index];
            hotkey.Key = key;
            hotkey.Modifier = modifiers;
            settings.Hotkeys[hotkey.Label] = new HotkeyBinding { Key = key, Modifiers = modifiers, Gamepad = hotkey.GamepadMask ?? 0 };
            UpdateState();
            return true;
        }

        public void SetReady(bool value) { if (ready != value) { ready = value; UpdateState(); } }
        public void SetEditing(bool value) { if (editing != value) { editing = value; UpdateState(); } }

        private void UpdateState()
        {
            generation++;
            heldActions.Clear();
            PublishMovement();
            repeatTimer.Stop();
            IsActive = false;
            lastForegroundAllowed = foregroundAllowed();
            if (!disposed && ready && Enabled) focusTimer.Start();
            else focusTimer.Stop();
            if (disposed) Status = "Stopped.";
            else if (!Enabled) Status = "Disabled.";
            else if (!ready || !gameAvailable()) Status = "Helper or game unavailable.";
            else if (editing) Status = "Editing binding.";
            else if (!lastForegroundAllowed) Status = "Game or helper is not active.";
            else if (Hotkeys.All(h => h.Key == 0 && GamepadMaskOf(h) == 0)) Status = "No keys assigned.";
            else
            {
                try { listener.Start(); IsActive = true; Status = "Active."; }
                catch (Exception ex) { Status = "Input unavailable."; System.Diagnostics.Trace.WriteLine(ex); }
            }
            if (!IsActive) listener.Stop();
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnKeyPressed(uint key, KeyModifiers modifiers)
        {
            if (!IsActive || !CanUseCameraInput) return;
            Trigger(Hotkeys.Where(h => h.Key != 0 && h.Key == key && MatchesModifiers(h, modifiers) && Allows(h)).ToArray());
        }

        private void SetPadButtons(object controller, uint buttons)
        {
            lock (padButtons)
            {
                if (buttons == 0) padButtons.Remove(controller);
                else padButtons[controller] = buttons;
            }
        }

        private void UpdateGamepad()
        {
            if (disposed) return;
            uint buttons = 0;
            lock (padButtons) foreach (var value in padButtons.Values) buttons |= value;
            uint pressed = buttons & ~gamepadButtons;
            if (buttons == gamepadButtons) return;
            gamepadButtons = buttons;
            GamepadButtonsChanged?.Invoke(buttons);
            if (heldActions.RemoveWhere(h => !IsHeld(h)) > 0)
            {
                PublishMovement();
                if (heldActions.Count == 0) repeatTimer.Stop();
            }
            if (pressed == 0 || !IsActive || !CanUseCameraInput) return;
            var matches = Hotkeys.Where(h => GamepadMaskOf(h) != 0 && (buttons & GamepadMaskOf(h)) == GamepadMaskOf(h) &&
                (pressed & GamepadMaskOf(h)) != 0 && Allows(h)).ToArray();
            // The most specific combination wins, so Back+A does not also run a hotkey bound to A.
            Trigger(matches.Where(h => !matches.Any(other => GamepadMaskOf(other) != GamepadMaskOf(h) &&
                (GamepadMaskOf(other) & GamepadMaskOf(h)) == GamepadMaskOf(h))).ToArray());
        }

        private static uint GamepadMaskOf(Hotkey hotkey) => hotkey.GamepadMask ?? 0;

        private bool IsHeld(Hotkey hotkey) =>
            (hotkey.Key != 0 && listener.IsHeld(hotkey.Key) && MatchesModifiers(hotkey, listener.HeldModifiers)) ||
            (GamepadMaskOf(hotkey) != 0 && (gamepadButtons & GamepadMaskOf(hotkey)) == GamepadMaskOf(hotkey));

        private void Trigger(Hotkey[] matches)
        {
            if (matches.Length == 0) return;
            foreach (var hotkey in matches.Where(h => h.RepeatWhileHeld))
            {
                heldActions.Add(hotkey);
                repeatTimer.Start();
            }
            PublishMovement();
            int pendingGeneration = generation;
            // Memory operations run later on the UI thread, outside the keyboard hook.
            dispatch(() =>
            {
                foreach (var hotkey in matches.Where(h => !h.CameraSpeedBoost && !h.PositionSpeedBoost && (!h.RepeatWhileHeld || (h.CameraAxis < 0 && h.PositionAxis < 0))))
                {
                    if (pendingGeneration != generation || !IsActive || !CanUseCameraInput) return;
                    if (Allows(hotkey)) hotkey.Callback();
                }
            });
        }

        private void OnKeyReleased(uint key)
        {
            heldActions.RemoveWhere(h => h.Key == key && !IsHeld(h));
            PublishMovement();
            if (heldActions.Count == 0) repeatTimer.Stop();
        }

        private void RepeatHeldActions()
        {
            if (!IsActive || !CanUseCameraInput)
            {
                heldActions.Clear();
                PublishMovement();
                repeatTimer.Stop();
                return;
            }
            foreach (var action in heldActions.ToArray())
            {
                if (!Allows(action) || !IsHeld(action)) heldActions.Remove(action);
                else if (action.CameraAxis < 0 && action.PositionAxis < 0 && !action.CameraSpeedBoost && !action.PositionSpeedBoost) action.Callback();
            }
            PublishMovement();
            if (heldActions.Count == 0) repeatTimer.Stop();
        }

        private void PublishMovement()
        {
            bool nextPositionBoost = heldActions.Any(action => action.PositionSpeedBoost);
            if (positionSpeedBoost != nextPositionBoost)
            {
                positionSpeedBoost = nextPositionBoost;
                PositionSpeedBoostChanged?.Invoke(positionSpeedBoost);
            }
            var nextPosition = new int[3];
            foreach (var action in heldActions)
                if (action.PositionAxis >= 0 && action.PositionAxis < 3) nextPosition[action.PositionAxis] += action.PositionDirection;
            for (int i = 0; i < 3; i++) nextPosition[i] = Math.Sign(nextPosition[i]);
            if (!nextPosition.SequenceEqual(positionMovement))
            {
                positionMovement = nextPosition;
                PositionMovementChanged?.Invoke((int[])nextPosition.Clone());
            }
            bool nextBoost = heldActions.Any(action => action.CameraSpeedBoost);
            if (speedBoost != nextBoost) { speedBoost = nextBoost; CameraSpeedBoostChanged?.Invoke(speedBoost); }
            var next = new int[5];
            foreach (var action in heldActions)
                if (action.CameraAxis >= 0 && action.CameraAxis < next.Length) next[action.CameraAxis] += action.CameraDirection;
            for (int i = 0; i < next.Length; i++) next[i] = Math.Sign(next[i]);
            if (next.SequenceEqual(movement)) return;
            movement = next;
            CameraMovementChanged?.Invoke((int[])next.Clone());
        }

        private static bool MatchesModifiers(Hotkey hotkey, KeyModifiers modifiers) =>
            hotkey.Modifier == KeyModifiers.None || hotkey.Modifier == KeyIdentity.ModifiersForKey(hotkey.Key, modifiers);

        public void Dispose()
        {
            disposed = true;
            UpdateState();
            listener.KeyPressed -= OnKeyPressed;
            listener.KeyReleased -= OnKeyReleased;
            gamepad.Dispose();
            playStation?.Dispose();
            repeatTimer.Dispose();
            focusTimer.Dispose();
            listener.Dispose();
        }
    }
}
