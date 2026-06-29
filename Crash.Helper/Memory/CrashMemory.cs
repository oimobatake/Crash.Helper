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
		private Process hookedProcess;
		private readonly LevelNameCodeInjector levelNameInjector;

		public CrashMemory() : base("CrashBandicootNSaneTrilogy")
		{
			levelNameInjector = new LevelNameCodeInjector("CrashBandicootNSaneTrilogy.exe");

			Lives = new GamePointer<int>(0x01AA27C8, 0x10);
			Masks = new GamePointer<int>(0x01A69A98, 0x30, 0x1E0);
			LoadMap = new GamePointer(StringEncodingMode.Utf8, 0x01A5C6D8, 0x20);

            InputX = new GamePointer<float>(0x01AC3C48, 0xB8, 0xC48, 0xCE0);
            InputY = new GamePointer<float>(0x01AC3C48, 0xB8, 0xC48, 0xCE4);
            CamInputX = new GamePointer<float>(0x01AC3C48, 0xB8, 0xC48, 0xE28);
            CamInputY = new GamePointer<float>(0x01AC3C48, 0xB8, 0xC48, 0xE2C);
            //Restart = new GamePointer<byte>(0x01A69A98, 0x18, 0x60, 0xE0, 0x730);
        }

		public GamePointer<int> Lives { get; }
		public GamePointer<int> Masks { get; }
		public GamePointer LoadMap{ get; }
		public GamePointer<float> InputX{ get; }
		public GamePointer<float> InputY{ get; }
		public GamePointer<float> CamInputX{ get; }
		public GamePointer<float> CamInputY{ get; }
		//public GamePointer<byte> Restart { get; }
		public bool IsReliableLevelWritePending => levelNameInjector.IsPendingOneShot;
		public string PendingReliableLevelName => levelNameInjector.PendingLevelName;

		public bool EnableReliableLevelWrite(string mapValue, out string error)
		{
			error = null;

			if (string.IsNullOrEmpty(mapValue))
			{
				error = "Map value is empty.";
				return false;
			}

			if (hookedProcess == null || hookedProcess.HasExited)
			{
				error = "Game process is not hooked.";
				return false;
			}

			levelNameInjector.Bind(hookedProcess);
			return levelNameInjector.Enable(mapValue, out error);
		}

		public bool DisableReliableLevelWrite(out string error)
		{
			return levelNameInjector.Disable(out error);
		}

		public bool TryConsumeReliableLevelWriteHit(out string levelName, out int hitCount, out string capturedSource, out string error)
		{
			return levelNameInjector.TryConsumeOneShotHit(out levelName, out hitCount, out capturedSource, out error);
		}

		public bool TryConsumeReliableLevelWriteHit(out string levelName, out string error)
		{
			return levelNameInjector.TryConsumeOneShotHit(out levelName, out _, out _, out error);
		}

        protected override void OnHook(Process process)
		{
			hookedProcess = process;
			Lives.Process = process;
			Masks.Process = process;
			LoadMap.Process = process;
			InputX.Process = process;
			InputY.Process = process;
			CamInputX.Process = process;
			CamInputY.Process = process;
			//Restart.Process = process;
			levelNameInjector.Bind(process);
        }

		protected override void OnUnhook()
		{
			try { levelNameInjector.Disable(out _); } catch { }
			levelNameInjector.Unbind();
			hookedProcess = null;

			Lives.Process = null;
			Masks.Process = null;
			LoadMap.Process = null;
			InputX?.Process = null;
			InputY?.Process = null;
			CamInputX?.Process = null;
			CamInputY?.Process = null;
			//Restart.Process = null;
        }

		public void Refresh()
		{
			Lives.Refresh();
			Masks.Refresh();
			LoadMap.Refresh();
			InputX?.Refresh();
			InputY?.Refresh();
			CamInputX?.Refresh();
			CamInputY?.Refresh();
			//Restart.Refresh();
        }
	}
}
