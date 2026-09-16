using System.Windows.Forms;

namespace Crash.Helper.Input
{
    internal static class HotkeyDisplay
    {
        public static string KeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)key - (int)Keys.D0).ToString();
            // Use the unshifted JIS key legends; modifiers are displayed separately.
            switch (key)
            {
                case Keys.Oemplus: return ";";
                case Keys.Oem1: return ":";
                case Keys.OemMinus: return "-";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.Oemtilde: return "@";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemQuotes: return "^";
                case Keys.OemPipe:
                case Keys.OemBackslash: return "\\";
                default: return key.ToString();
            }
        }
    }
}
