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
			Lives = new GamePointer<int>(0x01AA27C8, 0x10);
			Masks = new GamePointer<int>(0x01A69A98, 0x30, 0x1E0);
			LoadMap = new GamePointer(StringEncodingMode.Utf8, 0x01A5C6D8, 0x20);
			Restart = new GamePointer<byte>(0x01A69A98, 0x18, 0x60, 0xE0, 0x730);
        }

		public GamePointer<int> Lives { get; }
		public GamePointer<int> Masks { get; }
		public GamePointer LoadMap{ get; }
		public GamePointer<byte> Restart { get; }

        protected override void OnHook(Process process)
		{
			Lives.Process = process;
			Masks.Process = process;
			LoadMap.Process = process;
			Restart.Process = process;
        }

		protected override void OnUnhook()
		{
			Lives.Process = null;
			Masks.Process = null;
			LoadMap.Process = null;
			Restart.Process = null;
        }

		public void Refresh()
		{
			Lives.Refresh();
			Masks.Refresh();
			LoadMap.Refresh();
			Restart.Refresh();
        }
	}
}
