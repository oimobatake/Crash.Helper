using System;
using System.Windows.Forms;

namespace Crash.Helper.Controls
{
    // Repeats while pressed, and stops when capture, focus, or availability is lost.
    internal sealed class HoldButton : Button
    {
        private readonly Timer repeat = new Timer { Interval = 16 };
        private bool holding, suppressClick;
        internal Action HeldAction { get; set; }

        internal HoldButton() => repeat.Tick += (s, e) =>
        {
            if (holding && Enabled && Visible) HeldAction?.Invoke();
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
            if (Enabled) HeldAction?.Invoke();
            base.OnClick(e);
        }

        private void StartHolding()
        {
            if (!Enabled || !Visible) return;
            holding = suppressClick = true;
            repeat.Start();
            HeldAction?.Invoke();
        }

        private void StopHolding() { holding = false; repeat.Stop(); }

        protected override void OnLostFocus(EventArgs e) { StopHolding(); suppressClick = false; base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) StopHolding(); base.OnEnabledChanged(e); }
        protected override void OnVisibleChanged(EventArgs e) { if (!Visible) StopHolding(); base.OnVisibleChanged(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) repeat.Dispose();
            base.Dispose(disposing);
        }
    }
}
