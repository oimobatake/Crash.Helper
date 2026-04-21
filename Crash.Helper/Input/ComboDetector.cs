using System;
using System.Collections.Generic;

namespace Crash.Helper.Input
{
    public class ComboDetector
    {
        private ushort prevButtons = 0;
        private readonly Dictionary<ushort, Action> combos = new Dictionary<ushort, Action>();
        private readonly Dictionary<ushort, int> lastPressedTimestamps = new Dictionary<ushort, int>();
        private readonly int simultaneousWindowMs;

        public ComboDetector(int simultaneousWindowMs = 80)
        {
            this.simultaneousWindowMs = simultaneousWindowMs;
        }

        public void RegisterCombo(ushort mask, Action action)
        {
            combos[mask] = action;
        }

        public void Update(ushort currentButtons, int timestampMs)
        {
            ushort changed = (ushort)(currentButtons ^ prevButtons);

            if (changed != 0)
            {
                ushort pressed = (ushort)(changed & currentButtons);

                if (pressed != 0)
                {
                    for (ushort b = 1; b != 0; b <<= 1)
                    {
                        if ((pressed & b) != 0)
                        {
                            lastPressedTimestamps[b] = timestampMs;
                        }
                    }

                    foreach (var kv in combos)
                    {
                        ushort comboMask = kv.Key;
                        Action act = kv.Value;

                        if ((currentButtons & comboMask) == comboMask)
                        {
                            long minTs = long.MaxValue;
                            long maxTs = long.MinValue;
                            bool allKnown = true;

                            for (ushort b = 1; b != 0; b <<= 1)
                            {
                                if ((comboMask & b) != 0)
                                {
                                    if (!lastPressedTimestamps.TryGetValue(b, out int ts))
                                    {
                                        allKnown = false;
                                        break;
                                    }
                                    if (ts < minTs) minTs = ts;
                                    if (ts > maxTs) maxTs = ts;
                                }
                            }

                            if (allKnown && (maxTs - minTs) <= simultaneousWindowMs)
                            {
                                if (((prevButtons & comboMask) != comboMask) || (pressed & comboMask) != 0)
                                {
                                    act?.Invoke();
                                }
                            }
                        }
                    }
                }
            }

            prevButtons = currentButtons;
        }
    }
}
