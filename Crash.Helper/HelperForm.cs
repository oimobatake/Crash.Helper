using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Controls;
using Crash.Helper.Memory;

namespace Crash.Helper
{
	public partial class HelperForm : Form
	{
		private const int Framerate = 10;

		private CrashMemory memory;
		private DataControl dataControl;
        private LevelSelectorControl levelSelector;
        private HotkeyControl hotkeyControl;
		private InputDisplayControl inputDisplayControl;
		private TraceLogControl traceLogControl;
		private ProcessControl processControl;
		private Timer refreshTimer;
		private TraceListener uiTraceListener;

		public HelperForm()
		{
			InitializeComponent();
			memory = new CrashMemory();
			dataControl = new DataControl(memory);
			levelSelector = new LevelSelectorControl(memory, dataControl);
			hotkeyControl = new HotkeyControl(memory, dataControl);
			//inputDisplayControl = new InputDisplayControl(memory);
			//traceLogControl = new TraceLogControl();
			processControl = new ProcessControl(memory, dataControl, hotkeyControl, null, this);

			flowLayoutPanel.Controls.Add(processControl);
			flowLayoutPanel.Controls.Add(dataControl);
			flowLayoutPanel.Controls.Add(levelSelector);
			flowLayoutPanel.Controls.Add(hotkeyControl);
			//flowLayoutPanel.Controls.Add(inputDisplayControl);
			//flowLayoutPanel.Controls.Add(traceLogControl);
			flowLayoutPanel.Height--;

			//uiTraceListener = new UiTraceListener(traceLogControl);
			//Trace.Listeners.Add(uiTraceListener);
			//Trace.AutoFlush = true;
			//Trace.WriteLine("[HelperForm] UI trace listener attached.");

			refreshTimer = new Timer
            {
                Interval = (int)(1000f / Framerate),
            };

            refreshTimer.Tick += (sender, e) => { RefreshHelper(); };
            processControl.Rescan();
		}

		public bool RefreshEnabled
		{
			set
			{
				if (value)
				{
					refreshTimer.Start();
				}
				else
				{
					refreshTimer.Stop();
				}
			}
		}

		public void PrepareHelperForLaunch()
		{
			try { processControl?.PrepareForLaunch(); } catch { }
		}

		private void RefreshHelper()
		{
			if (!memory.HookProcess())
			{
				refreshTimer.Stop();
				processControl.OnUnhook();
				dataControl.Enabled = false;
                hotkeyControl.Enabled = false;
				//inputDisplayControl.Enabled = false;
				//inputDisplayControl.RefreshInputs();

				return;
			}

			memory.Refresh();
			TryCompleteReliableLevelWrite();
			//inputDisplayControl.RefreshInputs();
		}

		private void TryCompleteReliableLevelWrite()
		{
			if (!memory.IsReliableLevelWritePending)
			{
				return;
			}

			if (!memory.TryConsumeReliableLevelWriteHit(out var levelName, out var hitCount, out var capturedSource, out var hitError))
			{
				if (!string.IsNullOrEmpty(hitError))
				{
					Trace.WriteLine($"[HelperForm] Reliable write hit check failed: {hitError}");
				}

				return;
			}

			string currentMap = string.Empty;
			try { currentMap = memory.LoadMap.Read(); } catch { currentMap = string.Empty; }

			Trace.WriteLine($"[HelperForm] Reliable loadmap command completed. pendingMap='{levelName}' current='{currentMap}' hitCount={hitCount} capturedSource='{capturedSource}'");

			if (!memory.DisableReliableLevelWrite(out var disableError) && !string.IsNullOrEmpty(disableError))
			{
				Trace.WriteLine($"[HelperForm] Reliable write auto-disable failed: {disableError}");
			}

			try { levelSelector?.OnReliableLevelWriteCompleted(currentMap); } catch { }
		}

		private void HelperForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (dataControl != null)
			{
				dataControl.CurrentMapChanged -= DataControl_CurrentMapChanged;
			}

			if (uiTraceListener != null)
			{
				Trace.Listeners.Remove(uiTraceListener);
				uiTraceListener.Dispose();
				uiTraceListener = null;
			}

			hotkeyControl.UnregisterHotkeys();
		}

		private sealed class UiTraceListener : TraceListener
		{
			private readonly TraceLogControl logControl;

			public UiTraceListener(TraceLogControl logControl)
			{
				this.logControl = logControl;
			}

			public override void Write(string message)
			{
				try { logControl?.AppendLog(message); } catch { }
			}

			public override void WriteLine(string message)
			{
				try { logControl?.AppendLog(message); } catch { }
			}
		}

		private void DataControl_CurrentMapChanged(object sender, string mapValue)
		{
			try
			{
				levelSelector?.SyncSelectionByMap(mapValue);
			}
			catch { }
		}
	}
}
