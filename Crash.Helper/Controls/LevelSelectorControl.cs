using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Crash.Helper.Memory;


namespace Crash.Helper.Controls
{
    public class LevelSelectorControl : UserControl
    {
        private ComboBox combo;
        private Button lockButton;
        private Button stopButton;
        private Button launchButton;
        private DataControl dataControl;

        private static readonly List<string> LevelOrder = new List<string>
        {
            // Full order copied from Lua
            "Crash 1 - The Wumpa Islands",
            "Crash 1 - N. Sanity Beach",
            "Crash 1 - Jungle Rollers",
            "Crash 1 - The Great Gate",
            "Crash 1 - Boulders",
            "Crash 1 - Upstream",
            "Crash 1 - Papu Papu",
            "Crash 1 - Rolling Stones",
            "Crash 1 - Hog Wild",
            "Crash 1 - Native Fortress",
            "Crash 1 - Up the Creek",
            "Crash 1 - Ripper Roo",
            "Crash 1 - The Lost City",
            "Crash 1 - Temple Ruins",
            "Crash 1 - Road to Nowhere",
            "Crash 1 - Boulder Dash",
            "Crash 1 - Whole Hog",
            "Crash 1 - Sunset Vista",
            "Crash 1 - Koala Kong",
            "Crash 1 - Heavy Machinery",
            "Crash 1 - Cortex Power",
            "Crash 1 - Generator Room",
            "Crash 1 - Toxic Waste",
            "Crash 1 - Pinstripe Potoroo",
            "Crash 1 - The High Road",
            "Crash 1 - Slippery Climb",
            "Crash 1 - Lights Out",
            "Crash 1 - Fumbling in the Dark",
            "Crash 1 - Jaws of Darkness",
            "Crash 1 - Castle Machinery",
            "Crash 1 - Dr. Nitrus Brio",
            "Crash 1 - The Lab",
            "Crash 1 - The Great Hall",
            "Crash 1 - Dr. Neo Cortex",
            "Crash 1 - Stormy Ascent",

            "Crash 2 - The Warp Room",
            "Crash 2 - Turtle Woods",
            "Crash 2 - Snow Go",
            "Crash 2 - Hang Eight",
            "Crash 2 - The Pits",
            "Crash 2 - Crash Dash",
            "Crash 2 - Ripper Roo",
            "Crash 2 - Snow Biz",
            "Crash 2 - Air Crash",
            "Crash 2 - Bear It",
            "Crash 2 - Crash Crush",
            "Crash 2 - The Eel Deal",
            "Crash 2 - Komodo Brothers",
            "Crash 2 - Plant Food",
            "Crash 2 - Sewer or Later",
            "Crash 2 - Bear Down",
            "Crash 2 - Road to Ruin",
            "Crash 2 - Un-Bearable",
            "Crash 2 - Tiny Tiger",
            "Crash 2 - Hangin' Out",
            "Crash 2 - Diggin' It",
            "Crash 2 - Cold Hard Crash",
            "Crash 2 - Ruination",
            "Crash 2 - Bee-Having",
            "Crash 2 - Dr. N. Gin",
            "Crash 2 - Piston It Away",
            "Crash 2 - Rock It",
            "Crash 2 - Night Fight",
            "Crash 2 - Pack Attack",
            "Crash 2 - Spaced Out",
            "Crash 2 - Dr. Neo Cortex",
            "Crash 2 - Totally Bear",
            "Crash 2 - Totally Fly",

            "Crash 3 - The Time Twister",
            "Crash 3 - Toad Village",
            "Crash 3 - Under Pressure",
            "Crash 3 - Orient Express",
            "Crash 3 - Bone Yard",
            "Crash 3 - Makin' Waves",
            "Crash 3 - Tiny Tiger",
            "Crash 3 - Gee Wiz",
            "Crash 3 - Hang'em High",
            "Crash 3 - Hog Ride",
            "Crash 3 - Tomb Time",
            "Crash 3 - Midnight Run",
            "Crash 3 - Dingodile",
            "Crash 3 - Dino Might!",
            "Crash 3 - Deep Trouble",
            "Crash 3 - High Time",
            "Crash 3 - Road Crash",
            "Crash 3 - Double Header",
            "Crash 3 - Dr. N. Tropy",
            "Crash 3 - Sphynxinator",
            "Crash 3 - Bye Bye Blimps",
            "Crash 3 - Tell No Tales",
            "Crash 3 - Future Frenzy",
            "Crash 3 - Tomb Wader",
            "Crash 3 - Dr. N. Gin",
            "Crash 3 - Gone Tomorrow",
            "Crash 3 - Orange Asphalt",
            "Crash 3 - Flaming Passion",
            "Crash 3 - Mad Bombers",
            "Crash 3 - Bug Lite",
            "Crash 3 - Dr. Neo Cortex",
            "Crash 3 - Ski Crazed",
            "Crash 3 - Area 51?",
            "Crash 3 - Rings of Power",
            "Crash 3 - Hot Coco",
            "Crash 3 - Eggipus Rex",
            "Crash 3 - Future Tense"
        };

        public static readonly Dictionary<string, string> Levels = new Dictionary<string, string>
        {
            ["Crash 1 - The Wumpa Islands"]    = "loadmap crash1/l100_hub/l100_hub",
            ["Crash 1 - N. Sanity Beach"]      = "loadmap crash1/l101_nsanitybeach/l101_nsanitybeach",
            ["Crash 1 - Jungle Rollers"]       = "loadmap crash1/l102_junglerollers/l102_junglerollers",
            ["Crash 1 - The Great Gate"]       = "loadmap crash1/l103_thegreatgate/l103_thegreatgate",
            ["Crash 1 - Boulders"]             = "loadmap crash1/l104_boulders/l104_boulders",
            ["Crash 1 - Upstream"]             = "loadmap crash1/l105_upstream/l105_upstream",
            ["Crash 1 - Papu Papu"]            = "loadmap crash1/bosses/b101_papupapu/b101_papupapu",
            ["Crash 1 - Rolling Stones"]       = "loadmap crash1/l106_rollingstones/l106_rollingstones",
            ["Crash 1 - Hog Wild"]             = "loadmap crash1/l107_hogwild/l107_hogwild",
            ["Crash 1 - Native Fortress"]      = "loadmap crash1/l108_nativefortress/l108_nativefortress",
            ["Crash 1 - Up the Creek"]         = "loadmap crash1/l109_upthecreek/l109_upthecreek",
            ["Crash 1 - Ripper Roo"]           = "loadmap crash1/bosses/b102_ripperroo/b102_ripperroo",
            ["Crash 1 - The Lost City"]        = "loadmap crash1/l110_thelostcity/l110_thelostcity",
            ["Crash 1 - Temple Ruins"]         = "loadmap crash1/l111_templeruins/l111_templeruins",
            ["Crash 1 - Road to Nowhere"]      = "loadmap crash1/l112_roadtonowhere/l112_roadtonowhere",
            ["Crash 1 - Boulder Dash"]         = "loadmap crash1/l113_boulderdash/l113_boulderdash",
            ["Crash 1 - Whole Hog"]            = "loadmap crash1/l114_wholehog/l114_wholehog",
            ["Crash 1 - Sunset Vista"]         = "loadmap crash1/l115_sunsetvista/l115_sunsetvista",
            ["Crash 1 - Koala Kong"]           = "loadmap crash1/bosses/b103_koalakong/b103_koalakong",
            ["Crash 1 - Heavy Machinery"]      = "loadmap crash1/l116_heavymachinery/l116_heavymachinery",
            ["Crash 1 - Cortex Power"]         = "loadmap crash1/l117_cortexpower/l117_cortexpower",
            ["Crash 1 - Generator Room"]       = "loadmap crash1/l118_generatorroom/l118_generatorroom",
            ["Crash 1 - Toxic Waste"]          = "loadmap crash1/l119_toxicwaste/l119_toxicwaste",
            ["Crash 1 - Pinstripe Potoroo"]    = "loadmap crash1/bosses/b104_pinstripepotoroo/b104_pinstripepotoroo",
            ["Crash 1 - The High Road"]        = "loadmap crash1/l120_thehighroad/l120_thehighroad",
            ["Crash 1 - Slippery Climb"]       = "loadmap crash1/l121_slipperyclimb/l121_slipperyclimb",
            ["Crash 1 - Lights Out"]           = "loadmap crash1/l122_lightsout/l122_lightsout",
            ["Crash 1 - Fumbling in the Dark"] = "loadmap crash1/l123_fumblinginthedark/l123_fumblinginthedark",
            ["Crash 1 - Jaws of Darkness"]     = "loadmap crash1/l124_jawsofdarkness/l124_jawsofdarkness",
            ["Crash 1 - Castle Machinery"]     = "loadmap crash1/l125_castlemachinery/l125_castlemachinery",
            ["Crash 1 - Dr. Nitrus Brio"]      = "loadmap crash1/bosses/b105_drnitrusbrio/b105_drnitrusbrio",
            ["Crash 1 - The Lab"]              = "loadmap crash1/l126_thelab/l126_thelab",
            ["Crash 1 - The Great Hall"]       = "loadmap crash1/l127_thegreathall/l127_thegreathall",
            ["Crash 1 - Dr. Neo Cortex"]       = "loadmap crash1/bosses/b106_drneocortex/b106_drneocortex",
            ["Crash 1 - Stormy Ascent"]        = "loadmap crash1/l128_stormyascent/l128_stormyascent",

            ["Crash 2 - The Warp Room"]        = "loadmap crash2/l200_hub/l200_hub",
            ["Crash 2 - Turtle Woods"]         = "loadmap crash2/l201_turtlewoods/l201_turtlewoods",
            ["Crash 2 - Snow Go"]              = "loadmap crash2/l202_snowgo/l202_snowgo",
            ["Crash 2 - Hang Eight"]           = "loadmap crash2/l203_hangeight/l203_hangeight",
            ["Crash 2 - The Pits"]             = "loadmap crash2/l204_thepits/l204_thepits",
            ["Crash 2 - Crash Dash"]           = "loadmap crash2/l205_crashdash/l205_crashdash",
            ["Crash 2 - Ripper Roo"]           = "loadmap crash2/bosses/b201_ripperroo/b201_ripperroo",
            ["Crash 2 - Snow Biz"]             = "loadmap crash2/l206_snowbiz/l206_snowbiz",
            ["Crash 2 - Air Crash"]            = "loadmap crash2/l207_aircrash/l207_aircrash",
            ["Crash 2 - Bear It"]              = "loadmap crash2/l208_bearit/l208_bearit",
            ["Crash 2 - Crash Crush"]          = "loadmap crash2/l209_crashcrush/l209_crashcrush",
            ["Crash 2 - The Eel Deal"]         = "loadmap crash2/l210_theeeldeal/l210_theeeldeal",
            ["Crash 2 - Komodo Brothers"]      = "loadmap crash2/bosses/b202_komodobrothers/b202_komodobrothers",
            ["Crash 2 - Plant Food"]           = "loadmap crash2/l211_plantfood/l211_plantfood",
            ["Crash 2 - Sewer or Later"]       = "loadmap crash2/l212_sewerorlater/l212_sewerorlater",
            ["Crash 2 - Bear Down"]            = "loadmap crash2/l213_beardown/l213_beardown",
            ["Crash 2 - Road to Ruin"]         = "loadmap crash2/l214_roadtoruin/l214_roadtoruin",
            ["Crash 2 - Un-Bearable"]          = "loadmap crash2/l215_unbearable/l215_unbearable",
            ["Crash 2 - Tiny Tiger"]           = "loadmap crash2/bosses/b203_tinytiger/b203_tinytiger",
            ["Crash 2 - Hangin' Out"]          = "loadmap crash2/l216_hanginout/l216_hanginout",
            ["Crash 2 - Diggin' It"]           = "loadmap crash2/l217_digginit/l217_digginit",
            ["Crash 2 - Cold Hard Crash"]      = "loadmap crash2/l218_coldhardcrash/l218_coldhardcrash",
            ["Crash 2 - Ruination"]            = "loadmap crash2/l219_ruination/l219_ruination",
            ["Crash 2 - Bee-Having"]           = "loadmap crash2/l220_beehaving/l220_beehaving",
            ["Crash 2 - Dr. N. Gin"]           = "loadmap crash2/bosses/b204_drngin/b204_drngin",
            ["Crash 2 - Piston It Away"]       = "loadmap crash2/l221_pistonitaway/l221_pistonitaway",
            ["Crash 2 - Rock It"]              = "loadmap crash2/l222_rockit/l222_rockit",
            ["Crash 2 - Night Fight"]          = "loadmap crash2/l223_nightfight/l223_nightfight",
            ["Crash 2 - Pack Attack"]          = "loadmap crash2/l224_packattack/l224_packattack",
            ["Crash 2 - Spaced Out"]           = "loadmap crash2/l225_spacedout/l225_spacedout",
            ["Crash 2 - Dr. Neo Cortex"]       = "loadmap crash２/bosses/b２05_drneocortex/b２05_drneocortex",
            ["Crash 2 - Totally Bear"]         = "loadmap crash２/l２２６_totallybear/l２２６_totallybear",
            ["Crash 2 - Totally Fly"]          = "loadmap crash２/l２２７_totallyfly/l２２７_totallyfly",

            ["Crash 3 - The Time Twister"]     = "loadmap crash3/l300_hub/l300_hub",
            ["Crash 3 - Toad Village"]         = "loadmap crash3/l301_toadvillage/l301_toadvillage",
            ["Crash 3 - Under Pressure"]       = "loadmap crash3/l302_underpressure/l302_underpressure",
            ["Crash 3 - Orient Express"]       = "loadmap crash3/l303_orientexpress/l303_orientexpress",
            ["Crash 3 - Bone Yard"]            = "loadmap crash3/l304_boneyard/l304_boneyard",
            ["Crash 3 - Makin' Waves"]         = "loadmap crash3/l305_makinwaves/l305_makinwaves",
            ["Crash 3 - Tiny Tiger"]           = "loadmap crash3/bosses/b301_tinytiger/b301_tinytiger",
            ["Crash 3 - Gee Wiz"]              = "loadmap crash3/l306_geewiz/l306_geewiz",
            ["Crash 3 - Hang'em High"]         = "loadmap crash3/l307_hangemhigh/l307_hangemhigh",
            ["Crash 3 - Hog Ride"]             = "loadmap crash3/l308_hogride/l308_hogride",
            ["Crash 3 - Tomb Time"]            = "loadmap crash3/l309_tombtime/l309_tombtime",
            ["Crash 3 - Midnight Run"]         = "loadmap crash3/l310_midnightrun/l310_midnightrun",
            ["Crash 3 - Dingodile"]            = "loadmap crash3/bosses/b302_dingodile/b302_dingodile",
            ["Crash 3 - Dino Might!"]          = "loadmap crash3/l311_dinomight/l311_dinomight",
            ["Crash 3 - Deep Trouble"]         = "loadmap crash3/l312_deeptrouble/l312_deeptrouble",
            ["Crash 3 - High Time"]            = "loadmap crash3/l313_hightime/l313_hightime",
            ["Crash 3 - Road Crash"]           = "loadmap crash3/l314_roadcrash/l314_roadcrash",
            ["Crash 3 - Double Header"]        = "loadmap crash3/l315_doubleheader/l315_doubleheader",
            ["Crash 3 - Dr. N. Tropy"]         = "loadmap crash3/bosses/b303_ntropy/b303_ntropy",
            ["Crash 3 - Sphynxinator"]         = "loadmap crash3/l316_sphynxinator/l316_sphynxinator",
            ["Crash 3 - Bye Bye Blimps"]       = "loadmap crash3/l317_byebyeblimps/l317_byebyeblimps",
            ["Crash 3 - Tell No Tales"]        = "loadmap crash3/l318_tellnotales/l318_tellnotales",
            ["Crash 3 - Future Frenzy"]        = "loadmap crash3/l319_futurefrenzy/l319_futurefrenzy",
            ["Crash 3 - Tomb Wader"]           = "loadmap crash3/l320_tombwader/l320_tombwader",
            ["Crash 3 - Dr. N. Gin"]           = "loadmap crash3/bosses/b304_ngin/b304_ngin",
            ["Crash 3 - Gone Tomorrow"]        = "loadmap crash3/l321_gonetomorrow/l321_gonetomorrow",
            ["Crash 3 - Orange Asphalt"]       = "loadmap crash3/l322_orangeasphalt/l322_orangeasphalt",
            ["Crash 3 - Flaming Passion"]      = "loadmap crash3/l323_flamingpassion/l323_flamingpassion",
            ["Crash 3 - Mad Bombers"]          = "loadmap crash3/l324_madbombers/l324_madbombers",
            ["Crash 3 - Bug Lite"]             = "loadmap crash3/l325_buglite/l325_buglite",
            ["Crash 3 - Dr. Neo Cortex"]       = "loadmap crash3/bosses/b305_neocortex/b305_neocortex",
            ["Crash 3 - Ski Crazed"]           = "loadmap crash3/l326_skicrazed/l326_skicrazed",
            ["Crash 3 - Area 51?"]             = "loadmap crash3/l328_area51/l328_area51",
            ["Crash 3 - Rings of Power"]       = "loadmap crash3/l330_ringsofpower/l330_ringsofpower",
            ["Crash 3 - Hot Coco"]             = "loadmap crash3/l331_hotcoco/l331_hotcoco",
            ["Crash 3 - Eggipus Rex"]          = "loadmap crash3/l332_eggipusrex/l332_eggipusrex",
            ["Crash 3 - Future Tense"]         = "loadmap crash3/l333_futuretense/l333_futuretense"
        };

        public LevelSelectorControl(DataControl dataControl)
        {
            this.dataControl = dataControl;

            this.AutoSize = true;
            this.MinimumSize = new Size(285, 137);
            this.Margin = new Padding(0, 5, 0, 0);
            var levelBox = new GroupBox { Text = "Level", Size = new Size(285, 137) };

            combo = new ComboBox { Left = 7, Top = 43, Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var name in LevelOrder) combo.Items.Add(name);
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            combo.SelectedIndexChanged += (s, e) => {
                try
                {
                    if (dataControl != null && dataControl.Enabled && dataControl.IsMapFrozen)
                    {
                        // if map freeze is active, selecting a new level should apply and re-freeze it
                        SetSelectedLevel(true);
                    }
                }
                catch { }
            };

            lockButton = new Button { Left = 7, Top = 73, Width = 80, Text = "Level Lock" };
            lockButton.Click += (s, e) => { SetSelectedLevel(true); };

            stopButton = new Button { Left = 92, Top = 73, Width = 80, Text = "Stop Lock" };
            stopButton.Click += (s, e) => { StopLock(); };

            launchButton = new Button { Left = 177, Top = 73, Width = 100, Text = "Launch Game" };
            launchButton.Click += (s, e) => { LaunchSelectedLevel(); };

            levelBox.Controls.Add(combo);
            levelBox.Controls.Add(lockButton);
            levelBox.Controls.Add(stopButton);
            levelBox.Controls.Add(launchButton);
            dataControl.AttachLevelControls(levelBox);
            this.Controls.Add(levelBox);

            ApplyAvailability(true, false);
        }

        public event Action<string> LaunchRequested;
        public int LaunchButtonRight => launchButton.Right;

        public void ApplyAvailability(bool helperEnabled, bool ready)
        {
            combo.Enabled = helperEnabled;
            lockButton.Enabled = ready;
            stopButton.Enabled = ready;
            launchButton.Enabled = true;
        }

        public void MoveSelection(int direction)
        {
            if (!dataControl.Enabled || !combo.Enabled) return;
            combo.SelectedIndex = Math.Max(0, Math.Min(combo.Items.Count - 1, combo.SelectedIndex + direction));
        }

        public void ToggleLock()
        {
            if (!dataControl.Enabled) return;
            if (dataControl.IsMapFrozen) StopLock();
            else SetSelectedLevel(true);
        }

        private void SetSelectedLevel(bool lockIt)
        {
            if (combo.SelectedItem == null) return;
            var display = combo.SelectedItem.ToString();
            if (!Levels.ContainsKey(display)) return;
            var map = Levels[display];
            var mapKey = Levels.Keys.FirstOrDefault(k => Levels[k] == map);
            try
            {
                dataControl.SetMapLock(map, mapKey, lockIt);
                // log action
                System.Diagnostics.Trace.WriteLine($"[LevelSelector] Set level '{display}' -> '{map}' (lock={lockIt})");
            }
            catch { }
        }

        private void StopLock()
        {
            try {
                dataControl.StopMapLock();
                System.Diagnostics.Trace.WriteLine("[LevelSelector] StopLock called");
            } catch { }
        }

        public void LaunchSelectedLevel()
        {
            if (combo.SelectedItem == null) return;
            var display = combo.SelectedItem.ToString();
            if (!Levels.ContainsKey(display)) return;
            var map = Levels[display];

            LaunchRequested?.Invoke(map);
        }
    }
}
