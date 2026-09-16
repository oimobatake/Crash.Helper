namespace Crash.Helper.Controls
{
    partial class HotkeyControl
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing) UnregisterHotkeys();
            base.Dispose(disposing);
        }
    }
}
