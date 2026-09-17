namespace Crash.Helper.Memory
{
    internal static class SecretLevelRules
    {
        public static int GetValue(string level, bool enabled)
        {
            switch (level)
            {
                case "Crash 2 - Air Crash": return enabled ? 1 : 0;
                case "Crash 2 - Snow Go": return enabled ? 2 : 0;
                case "Crash 2 - Road to Ruin": return enabled ? 3 : 0;
                case "Crash 2 - Totally Bear": return 4;
                case "Crash 2 - Totally Fly": return 5;
                case "Crash 3 - Ski Crazed": return 6;
                case "Crash 3 - Hang'em High": return enabled ? 7 : 0;
                case "Crash 3 - Area 51?": return 8;
                case "Crash 3 - Future Frenzy": return enabled ? 9 : 0;
                case "Crash 3 - Rings of Power": return 10;
                case "Crash 3 - Hot Coco": return 11;
                case "Crash 3 - Eggipus Rex": return 12;
                case "Crash 3 - Future Tense": return 1;
                default: return 0;
            }
        }

    }
}
