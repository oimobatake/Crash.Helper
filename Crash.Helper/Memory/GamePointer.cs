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
        private IntPtr cachedBaseAddress;
        private int cachedProcessId = -1;

		private bool justWritten;

		public GamePointer(params int[] offsets)
		{
			this.offsets = offsets;
		}
		
		public Process Process { get; set; }
		public event Action<T, T> OnValueChange;

        private bool TryGetBaseAddress(out IntPtr baseAddress)
        {
            baseAddress = IntPtr.Zero;
            if (Process == null || Process.HasExited) return false;

            if (cachedProcessId != Process.Id)
            {
                cachedProcessId = Process.Id;
                cachedBaseAddress = IntPtr.Zero;
            }

            try
            {
                var mm64 = Process.MainModule64();
                if (mm64 != null && mm64.BaseAddress != IntPtr.Zero)
                {
                    baseAddress = mm64.BaseAddress;
                    cachedBaseAddress = baseAddress;
                    return true;
                }
            }
            catch { }

            try
            {
                var mainModule = Process.MainModule;
                if (mainModule != null && mainModule.BaseAddress != IntPtr.Zero)
                {
                    baseAddress = mainModule.BaseAddress;
                    cachedBaseAddress = baseAddress;
                    return true;
                }
            }
            catch { }

            if (cachedBaseAddress != IntPtr.Zero)
            {
                baseAddress = cachedBaseAddress;
                return true;
            }

            return false;
        }

		public T Read()
		{
            if (!TryGetBaseAddress(out var baseAddress)) return default(T);
            try { return Process.Read<T>(baseAddress, offsets); }
            catch { return default(T); }
		}

		public void Write(T value)
		{
            if (!TryGetBaseAddress(out var baseAddress)) return;
            try { Process.Write(baseAddress, value, offsets); }
            catch { return; }
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
        private IntPtr cachedBaseAddress;
        private int cachedProcessId = -1;

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

        private bool TryGetBaseAddress(out IntPtr baseAddress)
        {
            baseAddress = IntPtr.Zero;
            if (Process == null || Process.HasExited) return false;

            if (cachedProcessId != Process.Id)
            {
                cachedProcessId = Process.Id;
                cachedBaseAddress = IntPtr.Zero;
            }

            try
            {
                var mm64 = Process.MainModule64();
                if (mm64 != null && mm64.BaseAddress != IntPtr.Zero)
                {
                    baseAddress = mm64.BaseAddress;
                    cachedBaseAddress = baseAddress;
                    return true;
                }
            }
            catch { }

            try
            {
                var mainModule = Process.MainModule;
                if (mainModule != null && mainModule.BaseAddress != IntPtr.Zero)
                {
                    baseAddress = mainModule.BaseAddress;
                    cachedBaseAddress = baseAddress;
                    return true;
                }
            }
            catch { }

            if (cachedBaseAddress != IntPtr.Zero)
            {
                baseAddress = cachedBaseAddress;
                return true;
            }

            return false;
        }

        public string Read()
        {
            if (!TryGetBaseAddress(out var baseAddress)) return string.Empty;
            // support multiple string formats
            try
            {
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
                    var buf = Process.Read(baseAddress, 256, offsets);
                    int len = 0;
                    while (len < buf.Length && buf[len] != 0) len++;
                    try { return Encoding.UTF8.GetString(buf, 0, len); } catch { return string.Empty; }
                }

                string s = Process.ReadUtf16Z(baseAddress, offsets);
                if (!string.IsNullOrEmpty(s)) return s;
                return Process.ReadAscii(baseAddress, offsets);
            }
            catch { return string.Empty; }
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
            if (!TryGetBaseAddress(out var baseAddress)) return;
            try { Process.Write(baseAddress, bytes, offsets); }
            catch { return; }

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
