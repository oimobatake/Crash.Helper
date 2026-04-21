using System;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using Crash.Helper.Input;

namespace Crash.Helper.Input
{
    // WinRT fallback listener using reflection to avoid compile-time WinRT dependency
    public class WinRTGamepadListener : IGamepadListener
    {
        private readonly Timer pollTimer;
        private dynamic[] prevStates = new dynamic[4];

        public event EventHandler<GamepadButtonEventArgs> ButtonPressed;
        public event EventHandler<GamepadButtonEventArgs> ButtonReleased;

        private readonly Type gamepadType;
        private readonly Type readingType;
        private readonly bool available;

        // cached reflection objects
        private readonly PropertyInfo gamepadsProp;
        private readonly MethodInfo getCurrentReadingMethod;

        public WinRTGamepadListener(int pollIntervalMs = 20)
        {
            try
            {
                // use reflection to access Windows.Gaming.Input.Gamepad
                var asm = Type.GetType("Windows.Gaming.Input.Gamepad, Windows, ContentType=WindowsRuntime");
                if (asm == null)
                {
                    available = false;
                    return;
                }

                gamepadType = asm;

                // cache commonly used reflection objects to avoid expensive lookups in Poll
                gamepadsProp = gamepadType.GetProperty("Gamepads");
                getCurrentReadingMethod = gamepadType.GetMethod("GetCurrentReading");

                if (gamepadsProp == null || getCurrentReadingMethod == null)
                {
                    Trace.TraceWarning("WinRTGamepadListener: required Gamepad members not found via reflection.");
                    available = false;
                    return;
                }

                readingType = getCurrentReadingMethod.ReturnType;

                // start poll timer
                pollTimer = new Timer(Poll, null, 0, Math.Max(8, pollIntervalMs));
                available = true;
            }
            catch (Exception ex)
            {
                Trace.TraceError("WinRTGamepadListener ctor exception: {0}", ex);
                available = false;
            }
        }

        private void Poll(object state)
        {
            try
            {
                if (gamepadsProp == null)
                {
                    // nothing to do
                    return;
                }

                // get list of gamepads (cached property)
                var list = gamepadsProp.GetValue(null, null) as IEnumerable;
                if (list == null) return;
                int idx = 0;
                foreach (var gp in list)
                {
                    if (getCurrentReadingMethod == null) break;

                    object reading = null;
                    try
                    {
                        reading = getCurrentReadingMethod.Invoke(gp, null);
                    }
                    catch (TargetInvocationException tie)
                    {
                        // WinRT invocation may fail if called on wrong thread or if underlying COM proxy is invalid
                        Trace.TraceError("WinRTGamepadListener: GetCurrentReading TargetInvocationException for index {0}: {1}", idx, tie.InnerException ?? tie);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("WinRTGamepadListener: GetCurrentReading exception for index {0}: {1}", idx, ex);
                        continue;
                    }

                    // If the method returns a reference type, it can be null; if value type, boxed instance will never be null
                    if (!getCurrentReadingMethod.ReturnType.IsValueType && reading == null) continue;

                    // reading has Buttons property (enum). Use reflection and Convert to get a stable ulong representation.
                    var buttonsProp = reading.GetType().GetProperty("Buttons");
                    if (buttonsProp == null)
                    {
                        Trace.TraceWarning("WinRTGamepadListener: reading has no Buttons property (index {0})", idx);
                        prevStates[idx] = reading;
                        idx++;
                        continue;
                    }

                    object buttonsObj = null;
                    try
                    {
                        buttonsObj = buttonsProp.GetValue(reading, null);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("WinRTGamepadListener: failed to read Buttons property (index {0}): {1}", idx, ex);
                        prevStates[idx] = reading;
                        idx++;
                        continue;
                    }

                    ulong buttons;
                    try
                    {
                        buttons = Convert.ToUInt64(buttonsObj);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("WinRTGamepadListener: failed to convert Buttons to ulong (index {0}): {1}", idx, ex);
                        prevStates[idx] = reading;
                        idx++;
                        continue;
                    }

                    ulong prev = 0;
                    if (idx < prevStates.Length && prevStates[idx] != null)
                    {
                        try
                        {
                            var prevButtonsProp = prevStates[idx].GetType().GetProperty("Buttons");
                            if (prevButtonsProp != null)
                            {
                                var prevButtonsObj = prevButtonsProp.GetValue(prevStates[idx], null);
                                prev = Convert.ToUInt64(prevButtonsObj);
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError("WinRTGamepadListener: failed to read previous Buttons (index {0}): {1}", idx, ex);
                            prev = 0;
                        }
                    }

                    ulong changed = (buttons ^ prev);
                    if (changed != 0)
                    {
                        ulong pressed = changed & buttons;
                        ulong released = changed & ~buttons;

                        if (pressed != 0)
                        {
                            foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
                            {
                                ulong mask = (ulong)(ushort)b;
                                if ((pressed & mask) != 0) ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(idx, b));
                            }
                        }

                        if (released != 0)
                        {
                            foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
                            {
                                ulong mask = (ulong)(ushort)b;
                                if ((released & mask) != 0) ButtonReleased?.Invoke(this, new GamepadButtonEventArgs(idx, b));
                            }
                        }
                    }

                    prevStates[idx] = reading;
                    idx++;
                }
            }
            catch (Exception ex)
            {
                // Do not silently swallow exceptions - trace them for diagnosis
                Trace.TraceError("WinRTGamepadListener.Poll exception: {0}", ex);
            }
        }

        public void Dispose()
        {
            try { pollTimer?.Dispose(); } catch (Exception ex) { Trace.TraceError("WinRTGamepadListener.Dispose exception: {0}", ex); }
        }
    }
}
