using System.Windows.Forms;

namespace Crash.Helper.Input
{
    internal static class HotkeyDisplay
    {
        public static string KeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)key - (int)Keys.D0).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return "Num" + ((int)key - (int)Keys.NumPad0);
            if ((uint)key == KeyIdentity.NumEnter) return "NumEnter";
            // Use the unshifted JIS key legends; modifiers are displayed separately.
            switch (key)
            {
                case Keys.Add: return "Num+";
                case Keys.Subtract: return "Num-";
                case Keys.Divide: return "Num/";
                case Keys.Multiply: return "Num*";
                case Keys.Decimal: return "Num.";
                case Keys.Enter: return "Enter";
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
