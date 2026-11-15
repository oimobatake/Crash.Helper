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
        // XInput button mask (GamepadButton values); zero means no controller binding.
        [DataMember] public uint Gamepad { get; set; }
        [DataMember(EmitDefaultValue = false)] public List<HotkeyBinding> Alternatives { get; set; }

        public HotkeyBinding Clone() => new HotkeyBinding
        {
            Key = Key, Modifiers = Modifiers, Gamepad = Gamepad,
            Alternatives = Alternatives?.FindAll(binding => binding != null).ConvertAll(binding => binding.Clone())
        };

        internal bool IsAssigned => Key != 0 || Gamepad != 0;
    }

    [DataContract]
    public sealed class HelperSettings
    {
        // Steam player and camera coordinates use the engine's original units.
        public const float DefaultPositionXYZSpeed = 30f;
        public const float DefaultCameraXYZSpeed = 30f;
        public const float DefaultCameraFollowVerticalDeadzone = 60f;
        // Per-second approach rate toward the follow target. Zero moves the camera instantly.
        public const float DefaultCameraFollowSpeed = 8f;
        public const string DefaultStartupLevel = "Crash 1 - The Wumpa Islands";
        public static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashHelper_Settings.json");
        [DataMember] public string SteamPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe");
        [DataMember] public bool HotkeysEnabled { get; set; } = true;
        [DataMember] public bool LevelImageEnabled { get; set; } = true;
        [DataMember] public bool SelectCurrentLevelOnFreeze { get; set; }
        [DataMember] public bool LegacyMaskOption { get; set; }
        [DataMember] public string StartupLevel { get; set; } = DefaultStartupLevel;
        [DataMember] public bool AdvancedControlsEnabled { get; set; }
        [DataMember] public bool CameraMoveWithPitch { get; set; } = true;
        [DataMember] public bool CameraMouseControl { get; set; }
        [DataMember] public bool CameraInvertMouseX { get; set; }
        [DataMember] public bool CameraInvertMouseY { get; set; }
        [DataMember] public float PositionXYZSpeed { get; set; } = DefaultPositionXYZSpeed;
        [DataMember] public bool ResetCameraWhenPositionTP { get; set; } = true;
        [DataMember] private int TransformUnitsVersion = 1;
        [DataMember] public float CameraXYZSpeed { get; set; } = DefaultCameraXYZSpeed;
        [DataMember] public float CameraYawPitchSpeed { get; set; } = 0.03f;
        [DataMember] public float CameraMouseXSensitivity { get; set; } = 0.001f;
        [DataMember] public float CameraMouseYSensitivity { get; set; } = 0.001f;
        [DataMember] public float CameraOrbitHorizontalStep { get; set; } = 5f;
        [DataMember] public float CameraOrbitVerticalStep { get; set; } = 5f;
        [DataMember] public float CameraFollowVerticalDeadzone { get; set; } = DefaultCameraFollowVerticalDeadzone;
        [DataMember] public float CameraFollowSpeed { get; set; } = DefaultCameraFollowSpeed;
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
            SelectCurrentLevelOnFreeze = source.SelectCurrentLevelOnFreeze;
            LegacyMaskOption = source.LegacyMaskOption;
            StartupLevel = source.StartupLevel;
            CameraMoveWithPitch = source.CameraMoveWithPitch;
            CameraMouseControl = source.CameraMouseControl;
            CameraInvertMouseX = source.CameraInvertMouseX;
            CameraInvertMouseY = source.CameraInvertMouseY;
            PositionXYZSpeed = source.PositionXYZSpeed;
            ResetCameraWhenPositionTP = source.ResetCameraWhenPositionTP;
            TransformUnitsVersion = source.TransformUnitsVersion;
            CameraXYZSpeed = source.CameraXYZSpeed;
            CameraYawPitchSpeed = source.CameraYawPitchSpeed;
            CameraMouseXSensitivity = source.CameraMouseXSensitivity;
            CameraMouseYSensitivity = source.CameraMouseYSensitivity;
            CameraOrbitHorizontalStep = source.CameraOrbitHorizontalStep;
            CameraOrbitVerticalStep = source.CameraOrbitVerticalStep;
            CameraFollowVerticalDeadzone = source.CameraFollowVerticalDeadzone;
            CameraFollowSpeed = source.CameraFollowSpeed;
            Hotkeys = new Dictionary<string, HotkeyBinding>();
            foreach (var entry in source.Hotkeys ?? new Dictionary<string, HotkeyBinding>())
                Hotkeys[entry.Key] = entry.Value?.Clone() ?? new HotkeyBinding();
        }

        public static HelperSettings Load()
        {
            if (!File.Exists(FilePath)) return new HelperSettings();
            using (var stream = File.OpenRead(FilePath))
            {
                var settings = (HelperSettings)new DataContractJsonSerializer(typeof(HelperSettings)).ReadObject(stream);
                if (settings == null) throw new InvalidDataException("Settings are empty.");
                if (settings.Hotkeys == null) settings.Hotkeys = new Dictionary<string, HotkeyBinding>();
                if (!Controls.LevelSelectorControl.Levels.ContainsKey(settings.StartupLevel ?? string.Empty))
                    settings.StartupLevel = DefaultStartupLevel;
                if (!IsMovementValueValid(settings.CameraXYZSpeed)) settings.CameraXYZSpeed = DefaultCameraXYZSpeed;
                if (!IsMovementValueValid(settings.PositionXYZSpeed)) settings.PositionXYZSpeed = DefaultPositionXYZSpeed;
                if (!IsMovementValueValid(settings.CameraYawPitchSpeed)) settings.CameraYawPitchSpeed = 0.03f;
                if (!IsMovementValueValid(settings.CameraMouseXSensitivity)) settings.CameraMouseXSensitivity = 0.001f;
                if (!IsMovementValueValid(settings.CameraMouseYSensitivity)) settings.CameraMouseYSensitivity = 0.001f;
                if (!IsMovementValueValid(settings.CameraOrbitHorizontalStep)) settings.CameraOrbitHorizontalStep = 5f;
                if (!IsMovementValueValid(settings.CameraOrbitVerticalStep)) settings.CameraOrbitVerticalStep = 5f;
                if (!IsMovementValueValid(settings.CameraFollowVerticalDeadzone)) settings.CameraFollowVerticalDeadzone = DefaultCameraFollowVerticalDeadzone;
                if (!IsMovementValueValid(settings.CameraFollowSpeed)) settings.CameraFollowSpeed = DefaultCameraFollowSpeed;
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
            LegacyMaskOption = false;
            StartupLevel = DefaultStartupLevel;
            CameraMoveWithPitch = true;
            TransformUnitsVersion = 0;
            PositionXYZSpeed = float.NaN;
            ResetCameraWhenPositionTP = true;
            CameraXYZSpeed = DefaultCameraXYZSpeed;
            CameraYawPitchSpeed = 0.03f;
            CameraMouseXSensitivity = CameraMouseYSensitivity = 0.001f;
            CameraOrbitHorizontalStep = CameraOrbitVerticalStep = 5f;
            CameraFollowVerticalDeadzone = float.NaN;
            CameraFollowSpeed = DefaultCameraFollowSpeed;
        }

        [OnDeserialized]
        private void UpgradeTransformUnits(StreamingContext context)
        {
            bool legacy = TransformUnitsVersion < 1;
            PositionXYZSpeed = !IsMovementValueValid(PositionXYZSpeed) ? DefaultPositionXYZSpeed : legacy ? Math.Min(1000, PositionXYZSpeed / 0.0254f) : PositionXYZSpeed;
            CameraFollowVerticalDeadzone = !IsMovementValueValid(CameraFollowVerticalDeadzone) ? DefaultCameraFollowVerticalDeadzone : legacy ? Math.Min(1000, CameraFollowVerticalDeadzone / 0.0254f) : CameraFollowVerticalDeadzone;
            // Update the previous defaults, while preserving customized values.
            if (Math.Abs(PositionXYZSpeed - 0.8f / 0.0254f) < 0.00001f) PositionXYZSpeed = DefaultPositionXYZSpeed;
            if (Math.Abs(CameraFollowVerticalDeadzone - 1.5f / 0.0254f) < 0.00001f) CameraFollowVerticalDeadzone = DefaultCameraFollowVerticalDeadzone;
            TransformUnitsVersion = 1;
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
                name != nameof(CameraMouseXSensitivity) && name != nameof(CameraMouseYSensitivity) && name != nameof(PositionXYZSpeed) &&
                name != nameof(CameraFollowVerticalDeadzone) && name != nameof(CameraFollowSpeed) &&
                name != nameof(CameraOrbitHorizontalStep) && name != nameof(CameraOrbitVerticalStep))
                throw new ArgumentException("Unknown movement setting.", nameof(name));
            SaveSingleSetting(name, "number", value.ToString("R", CultureInfo.InvariantCulture));
            // Apply the live value only after the single-property save succeeds.
            switch (name)
            {
                case nameof(CameraXYZSpeed): CameraXYZSpeed = value; break;
                case nameof(CameraYawPitchSpeed): CameraYawPitchSpeed = value; break;
                case nameof(CameraMouseXSensitivity): CameraMouseXSensitivity = value; break;
                case nameof(CameraMouseYSensitivity): CameraMouseYSensitivity = value; break;
                case nameof(CameraOrbitHorizontalStep): CameraOrbitHorizontalStep = value; break;
                case nameof(CameraOrbitVerticalStep): CameraOrbitVerticalStep = value; break;
                case nameof(PositionXYZSpeed): PositionXYZSpeed = value; break;
                case nameof(CameraFollowVerticalDeadzone): CameraFollowVerticalDeadzone = value; break;
                case nameof(CameraFollowSpeed): CameraFollowSpeed = value; break;
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
                if (document.Root.Element(nameof(TransformUnitsVersion))?.Value != "1")
                {
                    // Upgrade related values together, even when saving just one setting.
                    // Keep unknown JSON members and existing shortcut bindings intact.
                    var upgraded = Load();
                    foreach (var entry in new Dictionary<string, float>
                    {
                        [nameof(PositionXYZSpeed)] = upgraded.PositionXYZSpeed,
                        [nameof(CameraFollowVerticalDeadzone)] = upgraded.CameraFollowVerticalDeadzone,
                        [nameof(TransformUnitsVersion)] = 1
                    })
                    {
                        document.Root.Element(entry.Key)?.Remove();
                        document.Root.Add(new XElement(entry.Key, new XAttribute("type", "number"), entry.Value.ToString("R", CultureInfo.InvariantCulture)));
                    }
                }
            }
            else document = new XDocument(new XElement("root", new XAttribute("type", "object")));
            document.Root.Element("CameraFollowScale")?.Remove();
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
