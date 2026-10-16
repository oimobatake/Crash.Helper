using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crash.Helper.Memory
{
	public abstract class GameMemory
	{
		private Process process;

		private string processName;

		protected GameMemory(string processName)
		{
			this.processName = processName;
		}

		protected abstract void OnHook(Process process);
		protected abstract void OnUnhook();

		public bool ProcessHooked => process != null && !process.HasExited;

        // Detect a running game even when Helper enabled is off, without attaching to it.
        internal bool IsProcessRunning
        {
            get
            {
                if (ProcessHooked) return true;
                var candidates = Process.GetProcessesByName(processName);
                try { return candidates.Any(candidate => !candidate.HasExited); }
                finally { foreach (var candidate in candidates) candidate.Dispose(); }
            }
        }

		public bool HookProcess()
		{
			if (process != null && process.HasExited)
			{
				process = null;
				OnUnhook();

				return false;
			}

			if (process == null)
			{
				Process[] processes = Process.GetProcessesByName(processName);
				process = processes.Length == 0 ? null : processes[0];

				if (process == null || process.HasExited)
				{
					return false;
				}

				MemoryReader.Update64Bit(process);

				OnHook(process);
			}

			return process != null;
		}
	}
}
