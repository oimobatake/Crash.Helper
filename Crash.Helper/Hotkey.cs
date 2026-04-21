using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crash.Helper.Controls;
using Crash.Helper.Memory;
using Crash.Helper.Input;

namespace Crash.Helper
{
	[Flags]
	public enum KeyModifiers
	{
		None = 0,
		Alt = 1,
		Control = 2,
		Shift = 4,
		Win = 8
	}

	public class Hotkey
	{
		public Hotkey(string label, KeyModifiers modifier, uint key, Action callback)
		{
			Label = label;
            Modifier = modifier;
            Key = key;
			Callback = callback;
		}

		public KeyModifiers Modifier { get; set; }

		public uint Key { get; set; }

        // bitmask of GamepadButton values (wButtons). Use null when no binding.
        public ushort? GamepadMask { get; set; }

		public int ID { get; set; }
		
		public string Label { get; }

		public Action Callback { get; }

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();

			void AppendFunction(object value)
			{
				if (builder.Length > 0)
				{
					builder.Append(" + ");
				}

				builder.Append(value);
			}

			bool alt = (Modifier & KeyModifiers.Alt) > 0;
			bool control = (Modifier & KeyModifiers.Control) > 0;
			bool shift = (Modifier & KeyModifiers.Shift) > 0;

			if (alt)
			{
				AppendFunction("Alt");
			}

			if (control)
			{
				AppendFunction("Ctrl");
			}

			if (shift)
			{
				AppendFunction("Shift");
			}

			if (Key == (uint)System.Windows.Forms.Keys.Add)
			{
				AppendFunction('+');
			}
			else
			{
				AppendFunction((char)Key);
			}

            if (GamepadMask.HasValue && GamepadMask.Value != 0)
            {
                // expand mask into names
                var names = new List<string>();
                foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
                {
                    ushort mask = (ushort)b;
                    if ((GamepadMask.Value & mask) != 0)
                    {
                        names.Add(b.ToString());
                    }
                }
                if (names.Count > 0)
                {
                    AppendFunction(string.Join("+", names));
                }
            }
			

			return builder.ToString();
		}
	}
}
