using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;

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
        [DataMember] public bool LevelImageEnabled { get; set; } = true;
        [DataMember] public bool AdvancedControlsEnabled { get; set; }
        [DataMember] public bool CameraMoveWithPitch { get; set; } = true;
        [DataMember] public bool CameraMouseControl { get; set; }
        [DataMember] public bool CameraInvertMouseY { get; set; }
        [DataMember] public float PositionXYZSpeed { get; set; } = 0.8f;
        [DataMember] public float CameraXYZSpeed { get; set; } = 30f;
        [DataMember] public float CameraYawPitchSpeed { get; set; } = 0.03f;
        [DataMember] public float CameraMouseXSensitivity { get; set; } = 0.002f;
        [DataMember] public float CameraMouseYSensitivity { get; set; } = 0.002f;
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
            LevelImageEnabled = source.LevelImageEnabled;
            CameraMoveWithPitch = source.CameraMoveWithPitch;
            CameraMouseControl = source.CameraMouseControl;
            CameraInvertMouseY = source.CameraInvertMouseY;
            PositionXYZSpeed = source.PositionXYZSpeed;
            CameraXYZSpeed = source.CameraXYZSpeed;
            CameraYawPitchSpeed = source.CameraYawPitchSpeed;
            CameraMouseXSensitivity = source.CameraMouseXSensitivity;
            CameraMouseYSensitivity = source.CameraMouseYSensitivity;
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
                if (!IsMovementValueValid(settings.CameraXYZSpeed)) settings.CameraXYZSpeed = 30f;
                if (!IsMovementValueValid(settings.PositionXYZSpeed)) settings.PositionXYZSpeed = 0.8f;
                if (!IsMovementValueValid(settings.CameraYawPitchSpeed)) settings.CameraYawPitchSpeed = 0.03f;
                if (!IsMovementValueValid(settings.CameraMouseXSensitivity)) settings.CameraMouseXSensitivity = 0.002f;
                if (!IsMovementValueValid(settings.CameraMouseYSensitivity)) settings.CameraMouseYSensitivity = 0.002f;
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
            LevelImageEnabled = true;
            CameraMoveWithPitch = true;
            PositionXYZSpeed = 0.8f;
            CameraXYZSpeed = 30f;
            CameraYawPitchSpeed = 0.03f;
            CameraMouseXSensitivity = CameraMouseYSensitivity = 0.002f;
        }

        public static void SaveAdvancedControls(bool enabled)
        {
            SaveSingleSetting(nameof(AdvancedControlsEnabled), "boolean", enabled ? "true" : "false");
        }

        private static bool IsMovementValueValid(float value) => !float.IsNaN(value) && value >= 0 && value <= 1000;

        public void SaveMovementValue(string name, float value)
        {
            if (!IsMovementValueValid(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (name != nameof(CameraXYZSpeed) && name != nameof(CameraYawPitchSpeed) &&
                name != nameof(CameraMouseXSensitivity) && name != nameof(CameraMouseYSensitivity) && name != nameof(PositionXYZSpeed))
                throw new ArgumentException("Unknown movement setting.", nameof(name));
            SaveSingleSetting(name, "number", value.ToString("R", CultureInfo.InvariantCulture));
            // Apply the live value only after the single-property save succeeds.
            switch (name)
            {
                case nameof(CameraXYZSpeed): CameraXYZSpeed = value; break;
                case nameof(CameraYawPitchSpeed): CameraYawPitchSpeed = value; break;
                case nameof(CameraMouseXSensitivity): CameraMouseXSensitivity = value; break;
                case nameof(CameraMouseYSensitivity): CameraMouseYSensitivity = value; break;
                case nameof(PositionXYZSpeed): PositionXYZSpeed = value; break;
            }
        }

        private static void SaveSingleSetting(string name, string type, string value)
        {
            XDocument document;
            if (File.Exists(FilePath))
            {
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(File.ReadAllBytes(FilePath), XmlDictionaryReaderQuotas.Max))
                    document = XDocument.Load(reader);
                if (document.Root?.Attribute("type")?.Value != "object") throw new InvalidDataException("Settings must be a JSON object.");
            }
            else document = new XDocument(new XElement("root", new XAttribute("type", "object")));
            var setting = new XElement(name, new XAttribute("type", type), value);
            var previous = document.Root.Element(name);
            if (previous == null) document.Root.Add(setting);
            else previous.ReplaceWith(setting);
            // Preserve saved bindings (including legacy names) and all other JSON members.
            // Never serialize the live or draft Settings window from a single-property edit.
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

    }
}
