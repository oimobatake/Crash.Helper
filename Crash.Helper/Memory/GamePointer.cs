using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crash.Helper.Memory
{
    public enum StringEncodingMode
    {
        Auto,
        Ascii,
        Utf16,
        Utf8
    }

	public class GamePointer<T> where T : struct
	{
		private int[] offsets;
		private T currentValue;

		private bool justWritten;

		public GamePointer(params int[] offsets)
		{
			this.offsets = offsets;
		}
		
		public Process Process { get; set; }
		public event Action<T, T> OnValueChange;

		public T Read()
		{
			// Prefer MainModule64 when available for correct base address on 64-bit processes
			var baseAddress = IntPtr.Zero;
			try
			{
				var mm64 = Process.MainModule64();
				if (mm64 != null)
				{
					baseAddress = mm64.BaseAddress;
				}
			}
			catch { }
			if (baseAddress == IntPtr.Zero)
			{
				baseAddress = Process.MainModule.BaseAddress;
			}
			return Process.Read<T>(baseAddress, offsets);
		}

		public void Write(T value)
		{
			var baseAddress = IntPtr.Zero;
			try
			{
				var mm64 = Process.MainModule64();
				if (mm64 != null)
				{
					baseAddress = mm64.BaseAddress;
				}
			}
			catch { }
			if (baseAddress == IntPtr.Zero)
			{
				baseAddress = Process.MainModule.BaseAddress;
			}
			Process.Write(baseAddress, value, offsets);
			currentValue = value;
			justWritten = true;
		}

		public void Refresh()
		{
			T newValue = Read();

			if (!justWritten && !newValue.Equals(currentValue))
			{
				OnValueChange?.Invoke(currentValue, newValue);

				if (!justWritten)
				{
					currentValue = newValue;
				}
			}

			justWritten = false;
		}
    	}

    public class GamePointer
    {
        private int[] offsets;
        private string currentValue;

        private bool justWritten;
        private StringEncodingMode encodingMode = StringEncodingMode.Auto;

        public GamePointer(params int[] offsets)
        {
            this.offsets = offsets;
            this.encodingMode = StringEncodingMode.Auto;
        }

        public GamePointer(StringEncodingMode encoding, params int[] offsets)
        {
            this.offsets = offsets;
            this.encodingMode = encoding;
        }

        public Process Process { get; set; }
        public event Action<string, string> OnValueChange;

        public string Read()
        {
            var baseAddress = IntPtr.Zero;
            try
            {
                var mm64 = Process.MainModule64();
                if (mm64 != null)
                {
                    baseAddress = mm64.BaseAddress;
                }
            }
            catch { }
            if (baseAddress == IntPtr.Zero)
            {
                baseAddress = Process.MainModule.BaseAddress;
            }
            // support multiple string formats
            if (encodingMode == StringEncodingMode.Ascii)
            {
                return Process.ReadAscii(baseAddress, offsets);
            }
            else if (encodingMode == StringEncodingMode.Utf16)
            {
                return Process.ReadUtf16Z(baseAddress, offsets);
            }
            else if (encodingMode == StringEncodingMode.Utf8)
            {
                // fallback to reading raw bytes and decode as UTF8 until null
                // We'll reuse ReadAscii by reading bytes and decoding here
                // ReadAscii returns ASCII only; for UTF8 we need to read raw bytes.
                // We'll read a reasonable chunk and trim at null.
                var buf = Process.Read(baseAddress, 256, offsets);
                int len = 0;
                while (len < buf.Length && buf[len] != 0) len++;
                try { return Encoding.UTF8.GetString(buf, 0, len); } catch { return string.Empty; }
            }

            // Auto: try UTF-16 first (common on Windows), then fallback to ASCII
            string s = Process.ReadUtf16Z(baseAddress, offsets);
            if (!string.IsNullOrEmpty(s)) return s;
            return Process.ReadAscii(baseAddress, offsets);
        }

        public void Write(string value)
        {
            if (value == null) value = string.Empty;

            byte[] bytes;
            if (encodingMode == StringEncodingMode.Ascii)
            {
                bytes = Encoding.ASCII.GetBytes(value + "\0");
            }
            else if (encodingMode == StringEncodingMode.Utf16)
            {
                bytes = Encoding.Unicode.GetBytes(value + "\0");
            }
            else if (encodingMode == StringEncodingMode.Utf8)
            {
                bytes = Encoding.UTF8.GetBytes(value + "\0");
            }
            else
            {
                // Auto: choose ASCII if all chars are <= 127, otherwise UTF-8
                bool allAscii = true;
                foreach (char c in value)
                {
                    if (c > 127) { allAscii = false; break; }
                }
                bytes = allAscii ? Encoding.ASCII.GetBytes(value + "\0") : Encoding.UTF8.GetBytes(value + "\0");
            }
            var baseAddress = IntPtr.Zero;
            try
            {
                var mm64 = Process.MainModule64();
                if (mm64 != null)
                {
                    baseAddress = mm64.BaseAddress;
                }
            }
            catch { }
            if (baseAddress == IntPtr.Zero)
            {
                baseAddress = Process.MainModule.BaseAddress;
            }
            Process.Write(baseAddress, bytes, offsets);

            currentValue = value;
            justWritten = true;
        }

        public void Refresh()
        {
            string newValue = Read();

            if (!justWritten && !newValue.Equals(currentValue))
            {
                OnValueChange?.Invoke(currentValue, newValue);

                if (!justWritten)
                {
                    currentValue = newValue;
                }
            }

            justWritten = false;
        }
    }
}
