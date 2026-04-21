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
			Lives = new GamePointer<int>(0x1A08548, 0x38, 0x70, 0x90, 0xA0, 0x748);
			Masks = new GamePointer<int>(0x1A08548, 0xC0, 0X90, 0X738, 0X58, 0X450);
			LoadMap = new GamePointer(StringEncodingMode.Utf8, 0x01A5C6D8, 0x20);
			Restart = new GamePointer<byte>(0x01A69A98, 0x78, 0xA0, 0xB18);
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
