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
			PositionX = new GamePointer<float>(0x01A5C160, 0x18, 0x8, 0x80);
            PositionY = new GamePointer<float>(0x01A5C160, 0x18, 0x8, 0x84);
            PositionZ = new GamePointer<float>(0x01A5C160, 0x18, 0x8, 0x88);
            Flags = GameFlag.CreateAll();
            SecretLevel = new GamePointer<int>(0x01A69A98, 0x30, 0x1740);
            Lives = new GamePointer<int>(0x01AA27C8, 0x10);
			Masks = new GamePointer<int>(0x01A69A98, 0x30, 0x1E0);
			LoadMap = new GamePointer(StringEncodingMode.Utf8, 0x01A5C6D8, 0x18);
            CurrentLevel = new GamePointer(StringEncodingMode.Utf8, 0x01A5C6E0);
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
		//public GamePointer<byte> Restart { get; }

        protected override void OnHook(Process process)
		{
			PositionX.Process = process;
            PositionY.Process = process;
            PositionZ.Process = process;
            foreach (var flag in Flags) flag.Value.Process = process;
            SecretLevel.Process = process;
            Lives.Process = process;
			Masks.Process = process;
			LoadMap.Process = process;
            CurrentLevel.Process = process;
			//Restart.Process = process;
        }

		protected override void OnUnhook()
		{
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
