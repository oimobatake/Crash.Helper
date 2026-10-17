using System;
using System.Windows.Forms;

namespace Crash.Helper.Controls
{
    // Repeats while pressed, and stops when capture, focus, or availability is lost.
    internal sealed class HoldButton : Button
    {
        private readonly Timer repeat = new Timer { Interval = 16 };
        private bool holding, suppressClick, clickPulse;
        internal Action HeldAction { get; set; }
        internal Action<bool> HoldingChanged { get; set; }

        internal HoldButton() => repeat.Tick += (s, e) =>
        {
            if (clickPulse) StopHolding();
            else if (holding && Enabled && Visible) HeldAction?.Invoke();
            else StopHolding();
        };

        protected override void OnMouseDown(MouseEventArgs e)
        {
            suppressClick = false;
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) StartHolding();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) StopHolding();
            try { base.OnMouseUp(e); }
            finally { suppressClick = false; }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture) StopHolding();
            base.OnMouseCaptureChanged(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space && e.Modifiers == Keys.None && !holding) StartHolding();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) StopHolding();
            try { base.OnKeyUp(e); }
            finally { suppressClick = false; }
        }

        protected override void OnClick(EventArgs e)
        {
            if (suppressClick) { suppressClick = false; return; }
            if (Enabled)
            {
                if (HoldingChanged == null) HeldAction?.Invoke();
                else { StartHolding(); clickPulse = true; suppressClick = false; }
            }
            base.OnClick(e);
        }

        private void StartHolding()
        {
            if (holding || !Enabled || !Visible) return;
            clickPulse = false;
            holding = suppressClick = true;
            repeat.Start();
            HoldingChanged?.Invoke(true);
            HeldAction?.Invoke();
        }

        private void StopHolding()
        {
            bool wasHolding = holding;
            holding = clickPulse = false;
            repeat.Stop();
            if (wasHolding) HoldingChanged?.Invoke(false);
        }

        internal static HoldButton Adjustment(int top, int direction, string name, Action<bool> holdingChanged) => new HoldButton
        {
            Left = direction < 0 ? 152 : 182, Top = top, Width = 28, Height = 24,
            Image = direction < 0 ? Properties.Resources.Subtract : Properties.Resources.Add,
            AccessibleName = name, HoldingChanged = holdingChanged
        };

        protected override void OnLostFocus(EventArgs e) { StopHolding(); suppressClick = false; base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) StopHolding(); base.OnEnabledChanged(e); }
        protected override void OnVisibleChanged(EventArgs e) { if (!Visible) StopHolding(); base.OnVisibleChanged(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { StopHolding(); repeat.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
