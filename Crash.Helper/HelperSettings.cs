using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;

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
        [DataMember] public bool AdvancedControlsEnabled { get; set; }
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
            AdvancedControlsEnabled = source.AdvancedControlsEnabled;
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
                if (settings.Hotkeys.TryGetValue("Teleport", out legacyPositionBinding))
                {
                    if (!settings.Hotkeys.ContainsKey("TP position")) settings.Hotkeys["TP position"] = legacyPositionBinding;
                    settings.Hotkeys.Remove("Teleport");
                }
                settings.RenameBinding("Save position", "Position Save");
                settings.RenameBinding("TP position", "Position TP");
                settings.RenameBinding("Save camera", "Camera Save");
                settings.RenameBinding("TP camera", "Camera TP");
                settings.RenameBinding("Camera X+", "Camera Left");
                settings.RenameBinding("Camera X-", "Camera Right");
                settings.RenameBinding("Camera Y+", "Camera Forward");
                settings.RenameBinding("Camera Y-", "Camera Back");
                settings.RenameBinding("Camera Z+", "Camera Up");
                settings.RenameBinding("Camera Z-", "Camera Down");
                settings.RenameBinding("Camera Yaw+", "Camera Yaw (Left)");
                settings.RenameBinding("Camera Yaw-", "Camera Yaw (Right)");
                settings.RenameBinding("Camera Pitch+", "Camera Pitch (Down)");
                settings.RenameBinding("Camera Pitch-", "Camera Pitch (Up)");
                if (string.IsNullOrWhiteSpace(settings.SteamPath)) settings.SteamPath = new HelperSettings().SteamPath;
                return settings;
            }
        }

        public void Save()
        {
            SaveJson(writer => new DataContractJsonSerializer(typeof(HelperSettings)).WriteObject(writer, this));
        }

        [OnDeserializing]
        private void InitializeDefaults(StreamingContext context)
        {
            // A file created by the Advanced Controls button can contain only its own setting.
            HotkeysEnabled = true;
        }

        public static void SaveAdvancedControls(bool enabled)
        {
            XDocument document;
            if (File.Exists(FilePath))
            {
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(File.ReadAllBytes(FilePath), XmlDictionaryReaderQuotas.Max))
                    document = XDocument.Load(reader);
                if (document.Root?.Attribute("type")?.Value != "object") throw new InvalidDataException("Settings must be a JSON object.");
            }
            else document = new XDocument(new XElement("root", new XAttribute("type", "object")));
            var setting = new XElement(nameof(AdvancedControlsEnabled), new XAttribute("type", "boolean"), enabled ? "true" : "false");
            var previous = document.Root.Element(nameof(AdvancedControlsEnabled));
            if (previous == null) document.Root.Add(setting);
            else previous.ReplaceWith(setting);
            // Preserve saved bindings (including legacy names) and all other JSON members.
            // Never serialize the live or draft Settings window from this button.
            SaveJson(writer => document.Root.WriteTo(writer));
        }

        private static void SaveJson(Action<XmlDictionaryWriter> write)
        {
            string temporaryPath = FilePath + ".tmp";
            using (var stream = File.Create(temporaryPath))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true, "  "))
                write(writer);
            if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, null);
            else File.Move(temporaryPath, FilePath);
        }

        private void RenameBinding(string oldName, string newName)
        {
            HotkeyBinding binding;
            if (!Hotkeys.TryGetValue(oldName, out binding)) return;
            if (!Hotkeys.ContainsKey(newName)) Hotkeys[newName] = binding;
            Hotkeys.Remove(oldName);
        }
    }
}
