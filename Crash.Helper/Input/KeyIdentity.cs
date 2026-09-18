using System.Windows.Forms;

namespace Crash.Helper.Input
{
    internal static class KeyIdentity
    {
        // Keep existing virtual-key bindings unchanged; reserve a distinct value for extended Enter.
        internal const uint NumEnter = 0x1000D;
        internal static uint Encode(uint key, bool extended) => key == (uint)Keys.Enter && extended ? NumEnter : key;
        internal static int VirtualKey(uint key) => key == NumEnter ? (int)Keys.Enter : (int)key;

        internal static uint Normalize(uint key)
        {
            switch ((Keys)key)
            {
                case Keys.LShiftKey: case Keys.RShiftKey: return (uint)Keys.ShiftKey;
                case Keys.LControlKey: case Keys.RControlKey: return (uint)Keys.ControlKey;
                case Keys.LMenu: case Keys.RMenu: return (uint)Keys.Menu;
                case Keys.RWin: return (uint)Keys.LWin;
                default: return key;
            }
        }

        internal static KeyModifiers ModifiersForKey(uint key, KeyModifiers modifiers)
        {
            switch ((Keys)Normalize(key))
            {
                case Keys.ShiftKey: return modifiers & ~KeyModifiers.Shift;
                case Keys.ControlKey: return modifiers & ~KeyModifiers.Control;
                case Keys.Menu: return modifiers & ~KeyModifiers.Alt;
                case Keys.LWin: return modifiers & ~KeyModifiers.Win;
                default: return modifiers;
            }
        }
    }

    internal sealed class HotkeyTextBox : TextBox
    {
        internal uint PhysicalKey { get; private set; }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0100 || message.Msg == 0x0104)
                PhysicalKey = KeyIdentity.Encode((uint)message.WParam.ToInt64(), (message.LParam.ToInt64() & (1L << 24)) != 0);
            base.WndProc(ref message);
        }
    }
}
