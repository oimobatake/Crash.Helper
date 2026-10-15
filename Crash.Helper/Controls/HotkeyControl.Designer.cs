namespace Crash.Helper.Controls
{
    partial class HotkeyControl
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing) { manager.StatusChanged -= OnStatusChanged; manager.GamepadButtonsChanged -= OnGamepadButtonsChanged; }
            base.Dispose(disposing);
        }
    }
}
