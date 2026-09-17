using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Crash.Helper.Memory
{
	public class CrashMemory : GameMemory
	{
		public CrashMemory() : base("CrashBandicootNSaneTrilogy")
		{
			PositionX = new GamePointer<float>(GameMemoryProfile.Steam.Position[0]);
            PositionY = new GamePointer<float>(GameMemoryProfile.Steam.Position[1]);
            PositionZ = new GamePointer<float>(GameMemoryProfile.Steam.Position[2]);
            Flags = GameFlag.CreateAll();
            SecretLevel = new GamePointer<int>(GameMemoryProfile.Steam.SecretLevel);
            Lives = new GamePointer<int>(GameMemoryProfile.Steam.Lives);
			Masks = new GamePointer<int>(GameMemoryProfile.Steam.Masks);
			LoadMap = new GamePointer(StringEncodingMode.Utf8, GameMemoryProfile.Steam.LoadMap);
            CurrentLevel = new GamePointer(StringEncodingMode.Utf8, GameMemoryProfile.Steam.CurrentLevel);
			//Restart = new GamePointer<byte>(0x01A69A98, 0x18, 0x60, 0xE0, 0x730);
        }

		public GamePointer<float> PositionX { get; }
        public GamePointer<float> PositionY { get; }
        public GamePointer<float> PositionZ { get; }
        public IReadOnlyList<GameFlag> Flags { get; }
        public GamePointer<int> SecretLevel { get; }
        public GamePointer<int> Lives { get; }
		public GamePointer<int> Masks { get; }
		public GamePointer LoadMap{ get; }
        public GamePointer CurrentLevel { get; }
        internal GameMemoryProfile Profile { get; private set; }
        public string VersionName => Profile?.Name ?? "Unknown";
        public bool IsSupportedVersion => Profile != null;
		//public GamePointer<byte> Restart { get; }

        protected override void OnHook(Process process)
        {
            GameMemoryProfile profile = null;
            try { profile = GameMemoryProfile.FromModuleSize(process.MainModule.ModuleMemorySize); }
            catch (Exception ex) { Trace.WriteLine("[Version] " + ex.Message); }
            ApplyProfile(process, profile);
        }

        internal void ApplyProfile(Process process, GameMemoryProfile profile)
        {
            OnUnhook();
            Profile = profile;
            if (profile == null) return;
            // Keep pointer objects stable: controls and event handlers already reference them.
            PositionX.Configure(process, profile.Position[0]);
            PositionY.Configure(process, profile.Position[1]);
            PositionZ.Configure(process, profile.Position[2]);
            for (int i = 0; i < Flags.Count; i++) Flags[i].Value.Configure(process, profile.Flags[i]);
            SecretLevel.Configure(process, profile.SecretLevel);
            Lives.Configure(process, profile.Lives);
            Masks.Configure(process, profile.Masks);
            LoadMap.Configure(process, profile.LoadMap);
            CurrentLevel.Configure(process, profile.CurrentLevel);
        }

        protected override void OnUnhook()
		{
            Profile = null;
			PositionX.Process = null;
            PositionY.Process = null;
            PositionZ.Process = null;
            foreach (var flag in Flags) flag.Value.Process = null;
            SecretLevel.Process = null;
            Lives.Process = null;
			Masks.Process = null;
			LoadMap.Process = null;
            CurrentLevel.Process = null;
			//Restart.Process = null;
        }

		public void Refresh()
		{
			Lives.Refresh();
			Masks.Refresh();
			LoadMap.Refresh();
            CurrentLevel.Refresh();
			//Restart.Refresh();
        }
	}
}
