using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Crash.Helper
{
	public static class Program
	{
        [STAThread]
		public static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
            using (var helper = new HelperForm()) Application.Run(helper);
		}
	}
}
