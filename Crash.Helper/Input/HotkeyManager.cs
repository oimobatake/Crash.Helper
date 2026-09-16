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

        public void SetReady(bool value) { ready = value; UpdateState(); }
        public void SetEditing(bool value) { editing = value; UpdateState(); }

        private void UpdateState()
        {
            generation++;
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
            int pendingGeneration = generation;
            // Memory operations run later on the UI thread, outside the keyboard hook.
            dispatch(() =>
            {
                if (disposed || pendingGeneration != generation || !IsActive || !ready || editing || !Enabled || !gameAvailable()) return;
                hotkey.Callback();
            });
        }

        public void Dispose()
        {
            disposed = true;
            UpdateState();
            listener.KeyPressed -= OnKeyPressed;
            listener.Dispose();
        }
    }
}
