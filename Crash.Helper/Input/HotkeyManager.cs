using System;
using System.Collections.Generic;
using System.Linq;

namespace Crash.Helper.Input
{
    internal sealed class HotkeyManager : IDisposable
    {
        private readonly HelperSettings settings;
        private readonly Func<bool> gameAvailable;
        private readonly Action<Action> dispatch;
        private readonly KeyboardHotkeyListener listener = new KeyboardHotkeyListener();
        private readonly System.Windows.Forms.Timer repeatTimer = new System.Windows.Forms.Timer { Interval = 8 };
        private readonly HashSet<Hotkey> heldActions = new HashSet<Hotkey>();
        private int[] movement = new int[5];
        public event Action<int[]> CameraMovementChanged;
        private bool ready;
        private bool editing;
        private bool disposed;
        private int generation;
        public IReadOnlyList<Hotkey> Hotkeys { get; }
        public string Status { get; private set; }
        public bool IsActive { get; private set; }
        public event EventHandler StatusChanged;
        public bool Enabled
        {
            get => settings.HotkeysEnabled;
            set { settings.HotkeysEnabled = value; UpdateState(); }
        }

        public HotkeyManager(IReadOnlyList<Hotkey> hotkeys, HelperSettings settings, Func<bool> gameAvailable, Action<Action> dispatch)
        {
            Hotkeys = hotkeys;
            this.settings = settings;
            this.gameAvailable = gameAvailable;
            this.dispatch = dispatch;
            foreach (var hotkey in hotkeys)
            {
                HotkeyBinding binding;
                if (!settings.Hotkeys.TryGetValue(hotkey.Label, out binding) || binding == null)
                    settings.Hotkeys[hotkey.Label] = binding = new HotkeyBinding();
                hotkey.Key = binding.Key;
                hotkey.Modifier = binding.Modifiers;
            }
            listener.KeyPressed += OnKeyPressed;
            listener.KeyReleased += OnKeyReleased;
            repeatTimer.Tick += (s, e) => RepeatHeldActions();
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
                }
            }
            UpdateState();
        }

        public bool TrySetBinding(int index, uint key, KeyModifiers modifiers)
        {
            if (key != 0 && Hotkeys.Where((h, i) => i != index).Any(h => h.Key == key && h.Modifier == modifiers)) return false;
            var hotkey = Hotkeys[index];
            hotkey.Key = key;
            hotkey.Modifier = modifiers;
            settings.Hotkeys[hotkey.Label] = new HotkeyBinding { Key = key, Modifiers = modifiers };
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
            if (disposed) Status = "Stopped.";
            else if (!Enabled) Status = "Disabled.";
            else if (!ready || !gameAvailable()) Status = "Helper or game unavailable.";
            else if (editing) Status = "Editing binding.";
            else if (Hotkeys.All(h => h.Key == 0)) Status = "No keys assigned.";
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
            if (!IsActive) return;
            var hotkey = Hotkeys.FirstOrDefault(h => h.Key == key && h.Modifier == modifiers);
            if (hotkey == null) return;
            if (hotkey.RepeatWhileHeld)
            {
                heldActions.Add(hotkey);
                repeatTimer.Start();
                if (hotkey.CameraAxis >= 0) { PublishMovement(); return; }
            }
            int pendingGeneration = generation;
            // Memory operations run later on the UI thread, outside the keyboard hook.
            dispatch(() =>
            {
                if (disposed || pendingGeneration != generation || !IsActive || !ready || editing || !Enabled || !gameAvailable()) return;
                hotkey.Callback();
            });
        }

        private void OnKeyReleased(uint key)
        {
            heldActions.RemoveWhere(h => h.Key == key);
            PublishMovement();
            if (heldActions.Count == 0) repeatTimer.Stop();
        }

        private void RepeatHeldActions()
        {
            if (!IsActive || !ready || editing || !Enabled || !gameAvailable())
            {
                heldActions.Clear();
                PublishMovement();
                repeatTimer.Stop();
                return;
            }
            var modifiers = KeyboardHotkeyListener.CurrentModifiers;
            foreach (var action in heldActions.ToArray())
            {
                if (!listener.IsHeld(action.Key) || action.Modifier != modifiers) heldActions.Remove(action);
                else if (action.CameraAxis < 0) action.Callback();
            }
            PublishMovement();
            if (heldActions.Count == 0) repeatTimer.Stop();
        }

        private void PublishMovement()
        {
            var next = new int[5];
            foreach (var action in heldActions)
                if (action.CameraAxis >= 0 && action.CameraAxis < next.Length) next[action.CameraAxis] += action.CameraDirection;
            for (int i = 0; i < next.Length; i++) next[i] = Math.Sign(next[i]);
            if (next.SequenceEqual(movement)) return;
            movement = next;
            CameraMovementChanged?.Invoke((int[])next.Clone());
        }

        public void Dispose()
        {
            disposed = true;
            UpdateState();
            listener.KeyPressed -= OnKeyPressed;
            listener.KeyReleased -= OnKeyReleased;
            repeatTimer.Dispose();
            listener.Dispose();
        }
    }
}
