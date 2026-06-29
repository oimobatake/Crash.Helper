using System;
using System.Globalization;
using System.Windows.Forms;
using Crash.Helper.Memory;

namespace Crash.Helper.Controls
{
    public partial class InputDisplayControl : UserControl
    {
        private readonly CrashMemory memory;

        public InputDisplayControl(CrashMemory memory)
        {
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            InitializeComponent();
        }

        public InputDisplayControl()
        {
            InitializeComponent();
        }

        public void RefreshInputs()
        {
            if (memory == null || !memory.ProcessHooked)
            {
                inputXLabel.Text = "InputX: -";
                inputYLabel.Text = "InputY: -";
                camInputXLabel.Text = "CamInputX: -";
                camInputYLabel.Text = "CamInputY: -";
                return;
            }

            inputXLabel.Text = $"InputX: {memory.InputX.Read().ToString("0.000", CultureInfo.InvariantCulture)}";
            inputYLabel.Text = $"InputY: {memory.InputY.Read().ToString("0.000", CultureInfo.InvariantCulture)}";
            camInputXLabel.Text = $"CamInputX: {memory.CamInputX.Read().ToString("0.000", CultureInfo.InvariantCulture)}";
            camInputYLabel.Text = $"CamInputY: {memory.CamInputY.Read().ToString("0.000", CultureInfo.InvariantCulture)}";
        }
    }
}