using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Crash.Helper
{
    [DataContract]
    public sealed class HotkeyBinding
    {
        [DataMember] public uint Key { get; set; }
        [DataMember] public KeyModifiers Modifiers { get; set; }
    }

    [DataContract]
    public sealed class HelperSettings
    {
        public static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashHelper_Settings.json");
        [DataMember] public string SteamPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe");
        [DataMember] public bool HotkeysEnabled { get; set; } = true;
        [DataMember] public Dictionary<string, HotkeyBinding> Hotkeys { get; set; } = new Dictionary<string, HotkeyBinding>();

        public HelperSettings Clone()
        {
            var copy = new HelperSettings();
            copy.CopyFrom(this);
            return copy;
        }

        public void CopyFrom(HelperSettings source)
        {
            SteamPath = source.SteamPath;
            HotkeysEnabled = source.HotkeysEnabled;
            Hotkeys = new Dictionary<string, HotkeyBinding>();
            foreach (var entry in source.Hotkeys)
                Hotkeys[entry.Key] = new HotkeyBinding { Key = entry.Value.Key, Modifiers = entry.Value.Modifiers };
        }

        public static HelperSettings Load()
        {
            if (!File.Exists(FilePath)) return new HelperSettings();
            using (var stream = File.OpenRead(FilePath))
            {
                var settings = (HelperSettings)new DataContractJsonSerializer(typeof(HelperSettings)).ReadObject(stream);
                if (settings == null) throw new InvalidDataException("Settings are empty.");
                if (settings.Hotkeys == null) settings.Hotkeys = new Dictionary<string, HotkeyBinding>();
                // Preserve bindings saved before the position terminology change.
                HotkeyBinding legacyPositionBinding;
                if (settings.Hotkeys.TryGetValue("Save location", out legacyPositionBinding))
                {
                    if (!settings.Hotkeys.ContainsKey("Save position")) settings.Hotkeys["Save position"] = legacyPositionBinding;
                    settings.Hotkeys.Remove("Save location");
                }
                if (string.IsNullOrWhiteSpace(settings.SteamPath)) settings.SteamPath = new HelperSettings().SteamPath;
                return settings;
            }
        }

        public void Save()
        {
            string temporaryPath = FilePath + ".tmp";
            using (var stream = File.Create(temporaryPath))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true, "  "))
                new DataContractJsonSerializer(typeof(HelperSettings)).WriteObject(writer, this);
            if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, null);
            else File.Move(temporaryPath, FilePath);
        }
    }
}
