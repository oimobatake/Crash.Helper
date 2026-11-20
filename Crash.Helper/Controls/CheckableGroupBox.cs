using System;
using System.Drawing;
using System.Windows.Forms;

namespace Crash.Helper.Controls
{
    internal sealed class CheckableGroupBox : GroupBox
    {
        private readonly CheckBox heading;
        private readonly Control content;
        private bool updating;

        internal CheckableGroupBox(string title, Control content)
        {
            this.content = content;
            AccessibleName = title;
            heading = new CheckBox
            {
                Text = title, AutoSize = true, Location = new Point(7, 0),
                BackColor = SystemColors.Control, TabIndex = 0
            };
            heading.CheckedChanged += (sender, args) =>
            {
                if (!updating) CheckedChanged?.Invoke(heading.Checked);
            };
            content.TabIndex = 1;
            Controls.Add(content);
            Controls.Add(heading);
            heading.BringToFront();
        }

        internal event Action<bool> CheckedChanged;

        internal void SetState(bool isChecked, bool canToggle, bool contentEnabled)
        {
            updating = true;
            heading.Checked = isChecked;
            updating = false;
            heading.Enabled = canToggle;
            content.Enabled = isChecked && contentEnabled;
        }
    }
}
