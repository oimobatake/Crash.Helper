using System;
using System.Windows.Forms;

namespace Crash.Helper.Controls
{
    public partial class TraceLogControl : UserControl
    {
        private const int MaxLogLength = 30000;

        public TraceLogControl()
        {
            InitializeComponent();
        }

        public void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (IsDisposed) return;

            if (InvokeRequired)
            {
                try { BeginInvoke((Action)(() => AppendLog(message))); } catch { }
                return;
            }

            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";

            if (logTextBox.TextLength > MaxLogLength)
            {
                logTextBox.Text = logTextBox.Text.Substring(logTextBox.TextLength / 2);
            }

            logTextBox.AppendText(line + Environment.NewLine);
        }

        private void clearButton_Click(object sender, EventArgs e)
        {
            logTextBox.Clear();
        }
    }
}