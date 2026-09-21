using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crash.Helper.Updates;

namespace Crash.Helper
{
	public static class Program
	{
        [STAThread]
		public static void Main(string[] args)
		{
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 1 && args[0] == UpdateInstaller.Switch)
            {
                using (var progress = new UpdateDialog())
                {
                    progress.StartPosition = FormStartPosition.CenterScreen;
                    progress.Shown += async (s, e) =>
                    {
                        await Task.Run(() => UpdateInstaller.Run());
                        progress.Complete();
                    };
                    Application.Run(progress);
                }
                return;
            }
            if (args.Contains(UpdateInstaller.RecoverySwitch))
            {
                // A failed installation must not restart into another automatic update loop.
                using (var helper = new HelperForm()) Application.Run(helper);
                return;
            }
            using (var helper = new HelperForm())
            using (var updates = new UpdateCoordinator(helper))
            {
                Application.Run(helper);
                if (updates.Pending != null)
                {
                    try { UpdateInstaller.Start(updates.Pending); }
                    catch (Exception ex)
                    {
                        HelperLog.Error("Start update installer", ex);
                        MessageBox.Show("Could not start the update. The existing application has not been replaced.\n" + ex.Message,
                            "Crash Helper", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
		}
	}
}
