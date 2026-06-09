using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Memory;

namespace Crash.Helper.Controls
{
	public partial class ProcessControl : UserControl
	{
		private const int RetryTime = 10;
		private const int LaunchConfirmIntervalMs = 1000;
		private const int LaunchConfirmMaxAttempts = 60;

		private CrashMemory memory;
		private DataControl data;
        private HotkeyControl hotkeys;
		private HelperForm parent;
		private Timer processTimer;
		private Timer launchConfirmTimer;

		private int retryTimeRemaining;
		private int launchConfirmAttempts;
		private bool reenableHelperAfterLaunch;
		private bool scanning;
		private string filler;

		public ProcessControl(CrashMemory memory, DataControl data, HotkeyControl hotkeys, HelperForm parent)
		{
			this.memory = memory;
			this.data = data;
            this.hotkeys = hotkeys;
			this.parent = parent;

			InitializeComponent();

			processTimer = new Timer();
			processTimer.Interval = 1000;
			processTimer.Tick += (sender, e) =>
			{
				retryTimeRemaining--;

				if (retryTimeRemaining == 0)
				{
					Rescan();
				}
				else
				{
					processLabel.Text = $"Process {filler}. Retrying in {retryTimeRemaining}...";
				}
			};

			filler = "not found";
        }

        public ProcessControl()
        {
            InitializeComponent();
        }

        public void Rescan(bool skipFirstCheck = false)
		{
			if (!skipFirstCheck && memory.HookProcess())
			{
				processLabel.Text = "Process attached.";
				processLabel.ForeColor = Color.ForestGreen;
				processTimer?.Stop();

				data.Enabled = true;
                hotkeys.Enabled = true;
				parent.RefreshEnabled = true;
				scanning = false;
			}
			else
			{
				retryTimeRemaining = RetryTime;
				processLabel.Text = $"Process {filler}. Retrying in {RetryTime}...";
				processTimer.Start();
				scanning = true;
			}
		}

		public void OnUnhook()
		{
			filler = "lost";
			processLabel.ForeColor = SystemColors.ControlDarkDark;
			Rescan(true);
		}

		private void helperCheckbox_CheckedChanged(object sender, EventArgs e)
		{
			if (helperCheckbox.Checked)
			{
				StopLaunchConfirm();
				reenableHelperAfterLaunch = false;

				filler = "not found";
				Rescan();
			}
			else
			{
				processLabel.Text = "Helper is disabled.";
				processLabel.ForeColor = SystemColors.ControlDarkDark;

				if (scanning)
				{
					processTimer.Stop();
					scanning = false;
				}

				if (reenableHelperAfterLaunch)
				{
					StartLaunchConfirm();
				}
			}

			bool isReady = helperCheckbox.Checked && memory.ProcessHooked;

			data.Enabled = isReady;
            hotkeys.Enabled = isReady;
			parent.RefreshEnabled = isReady;
		}

		public void PrepareForLaunch()
		{
			if (memory.ProcessHooked)
			{
				return;
			}

			if (!helperCheckbox.Checked)
			{
				return;
			}

			launchConfirmAttempts = 0;
			reenableHelperAfterLaunch = true;
			helperCheckbox.Checked = false;
			processLabel.Text = "Launching game... waiting for process.";
			processLabel.ForeColor = SystemColors.ControlDarkDark;
		}

		private void StartLaunchConfirm()
		{
			StopLaunchConfirm();
			launchConfirmTimer = new Timer();
			launchConfirmTimer.Interval = LaunchConfirmIntervalMs;
			launchConfirmTimer.Tick += (sender, e) =>
			{
				try
				{
					if (!reenableHelperAfterLaunch)
					{
						StopLaunchConfirm();
						return;
					}

					if (memory.HookProcess())
					{
						StopLaunchConfirm();
						reenableHelperAfterLaunch = false;
						helperCheckbox.Checked = true;
						return;
					}

					launchConfirmAttempts++;
					if (launchConfirmAttempts >= LaunchConfirmMaxAttempts)
					{
						StopLaunchConfirm();
						reenableHelperAfterLaunch = false;
						processLabel.Text = "Launch not detected. Enable helper manually.";
						processLabel.ForeColor = SystemColors.ControlDarkDark;
					}
					else
					{
						processLabel.Text = $"Waiting for game process... ({LaunchConfirmMaxAttempts - launchConfirmAttempts}s)";
					}
				}
				catch { }
			};

			launchConfirmTimer.Start();
		}

		private void StopLaunchConfirm()
		{
			launchConfirmTimer?.Stop();
			launchConfirmTimer?.Dispose();
			launchConfirmTimer = null;
		}
	}
}
