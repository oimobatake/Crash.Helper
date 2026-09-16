using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Crash.Helper.Memory;

namespace Crash.Helper.Controls
{
    internal sealed class GameFlagControl : UserControl
    {
        private readonly CrashMemory memory;
        private readonly Func<bool> canEdit;
        private readonly Dictionary<GameFlag, CheckBox> checkboxes = new Dictionary<GameFlag, CheckBox>();
        private readonly Timer timer = new Timer { Interval = 100 };
        private bool refreshing;
        private bool initialized;

        public GameFlagControl(CrashMemory memory, Func<bool> canEdit)
        {
            this.memory = memory;
            this.canEdit = canEdit;
            AutoSize = true;
            MinimumSize = new System.Drawing.Size(480, 0);
            var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Dock = DockStyle.Fill };
            foreach (var group in memory.Flags.GroupBy(flag => flag.Group))
            {
                var box = new GroupBox { Text = group.Key, AutoSize = true, MinimumSize = new System.Drawing.Size(group.Key == "Color Gems" ? 140 : 320, 0), Padding = new Padding(10) };
                var rows = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
                foreach (var flag in group)
                {
                    var checkbox = new CheckBox { Text = flag.Name, AutoSize = true };
                    checkbox.CheckedChanged += (s, e) =>
                    {
                        if (!refreshing && canEdit() && memory.ProcessHooked) flag.Value.Write(checkbox.Checked);
                    };
                    checkboxes.Add(flag, checkbox);
                    rows.Controls.Add(checkbox);
                }
                box.Controls.Add(rows);
                layout.Controls.Add(box);
            }
            Controls.Add(layout);
            timer.Tick += (s, e) => RefreshValues();
            RefreshValues();
            timer.Start();
        }

        internal void RefreshValues()
        {
            Enabled = canEdit() && memory.ProcessHooked;
            if (!Enabled) { initialized = false; return; }
            // Keep the opening snapshot while the user edits; only clicks write values.
            if (initialized) return;
            refreshing = true;
            try
            {
                foreach (var entry in checkboxes) entry.Value.Checked = entry.Key.Value.Read();
                initialized = true;
            }
            finally { refreshing = false; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
