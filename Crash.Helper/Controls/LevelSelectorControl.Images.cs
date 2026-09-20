using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Crash.Helper.Controls
{
    public partial class LevelSelectorControl
    {
        // Explicit filenames also cover spelling differences between map names and artwork.
        private static readonly Dictionary<string, string> LevelImages = new Dictionary<string, string>
        {
            ["Crash 1 - The Wumpa Islands"] = "L100_TheWumpaIslands.png",
            ["Crash 1 - N. Sanity Beach"] = "L101_NSanityBeach.png",
            ["Crash 1 - Jungle Rollers"] = "L102_JungleRollers.png",
            ["Crash 1 - The Great Gate"] = "L103_TheGreatGate.png",
            ["Crash 1 - Boulders"] = "L104_Boulders.png",
            ["Crash 1 - Upstream"] = "L105_Upstream.png",
            ["Crash 1 - Papu Papu"] = "B101_PapuPapu.png",
            ["Crash 1 - Rolling Stones"] = "L106_RollingStones.png",
            ["Crash 1 - Hog Wild"] = "L107_HogWild.png",
            ["Crash 1 - Native Fortress"] = "L108_NativeFortress.png",
            ["Crash 1 - Up the Creek"] = "L109_UpTheCreek.png",
            ["Crash 1 - Ripper Roo"] = "B102_RipperRoo.png",
            ["Crash 1 - The Lost City"] = "L110_TheLostCity.png",
            ["Crash 1 - Temple Ruins"] = "L111_TempleRuins.png",
            ["Crash 1 - Road to Nowhere"] = "L112_RoadToNowhere.png",
            ["Crash 1 - Boulder Dash"] = "L113_BoulderDash.png",
            ["Crash 1 - Whole Hog"] = "L114_WholeHog.png",
            ["Crash 1 - Sunset Vista"] = "L115_SunsetVista.png",
            ["Crash 1 - Koala Kong"] = "B103_KoalaKong.png",
            ["Crash 1 - Heavy Machinery"] = "L116_HeavyMachinery.png",
            ["Crash 1 - Cortex Power"] = "L117_CortexPower.png",
            ["Crash 1 - Generator Room"] = "L118_GeneratorRoom.png",
            ["Crash 1 - Toxic Waste"] = "L119_ToxicWaste.png",
            ["Crash 1 - Pinstripe Potoroo"] = "B104_PinestripeProtoroo.png",
            ["Crash 1 - The High Road"] = "L120_TheHighRoad.png",
            ["Crash 1 - Slippery Climb"] = "L121_SlipperyClimb.png",
            ["Crash 1 - Lights Out"] = "L122_LightsOut.png",
            ["Crash 1 - Fumbling in the Dark"] = "L123_FumblingInTheDark.png",
            ["Crash 1 - Jaws of Darkness"] = "L124_JawsOfDarkness.png",
            ["Crash 1 - Castle Machinery"] = "L125_CastleMachinery.png",
            ["Crash 1 - Dr. Nitrus Brio"] = "B105_DrNitrusBrio.png",
            ["Crash 1 - The Lab"] = "L126_TheLab.png",
            ["Crash 1 - The Great Hall"] = "L127_TheGreatHall.png",
            ["Crash 1 - Dr. Neo Cortex"] = "B106_DrNeoCortex.png",
            ["Crash 1 - Stormy Ascent"] = "L128_StormyAscent.png",
            ["Crash 2 - The Warp Room"] = "L200_TheWarpRoom.png",
            ["Crash 2 - Turtle Woods"] = "L201_TurtleWoods.png",
            ["Crash 2 - Snow Go"] = "L202_SnowGo.png",
            ["Crash 2 - Hang Eight"] = "L203_HangEight.png",
            ["Crash 2 - The Pits"] = "L204_ThePits.png",
            ["Crash 2 - Crash Dash"] = "L205_CrashDash.png",
            ["Crash 2 - Ripper Roo"] = "B201_RipperRoo.png",
            ["Crash 2 - Snow Biz"] = "L206_SnowBiz.png",
            ["Crash 2 - Air Crash"] = "L207_AirCrash.png",
            ["Crash 2 - Bear It"] = "L208_BearIt.png",
            ["Crash 2 - Crash Crush"] = "L209_CrashCrush.png",
            ["Crash 2 - The Eel Deal"] = "L210_TheEelDeal.png",
            ["Crash 2 - Komodo Brothers"] = "B202_KomodoBros.png",
            ["Crash 2 - Plant Food"] = "L211_PlantFood.png",
            ["Crash 2 - Sewer or Later"] = "L212_SewerOrLater.png",
            ["Crash 2 - Bear Down"] = "L213_BearDown.png",
            ["Crash 2 - Road to Ruin"] = "L214_RoadToRuin.png",
            ["Crash 2 - Un-Bearable"] = "L215_UnBearable.png",
            ["Crash 2 - Tiny Tiger"] = "B203_TinyTiger.png",
            ["Crash 2 - Hangin' Out"] = "L216_HanginOut.png",
            ["Crash 2 - Diggin' It"] = "L217_DigginIt.png",
            ["Crash 2 - Cold Hard Crash"] = "L218_ColdHardCrash.png",
            ["Crash 2 - Ruination"] = "L219_Ruination.png",
            ["Crash 2 - Bee-Having"] = "L220_BeeHaving.png",
            ["Crash 2 - Dr. N. Gin"] = "B204_NGin.png",
            ["Crash 2 - Piston It Away"] = "L221_PistonItAway.png",
            ["Crash 2 - Rock It"] = "L222_RockIt.png",
            ["Crash 2 - Night Fight"] = "L223_NightFight.png",
            ["Crash 2 - Pack Attack"] = "L224_PackAttack.png",
            ["Crash 2 - Spaced Out"] = "L225_SpacedOut.png",
            ["Crash 2 - Dr. Neo Cortex"] = "B205_DrNeoCortex.png",
            ["Crash 2 - Totally Bear"] = "L226_TotallyBear.png",
            ["Crash 2 - Totally Fly"] = "L227_TotallyFly.png",
            ["Crash 3 - The Time Twister"] = "L300_TheTimeTwister.png",
            ["Crash 3 - Toad Village"] = "L301_ToadVillage.png",
            ["Crash 3 - Under Pressure"] = "L302_UnderPressure.png",
            ["Crash 3 - Orient Express"] = "L303_OrientExpress.png",
            ["Crash 3 - Bone Yard"] = "L304_BoneYard.png",
            ["Crash 3 - Makin' Waves"] = "L305_MakinWaves.png",
            ["Crash 3 - Tiny Tiger"] = "B301_TinyTiger.png",
            ["Crash 3 - Gee Wiz"] = "L306_GeeWiz.png",
            ["Crash 3 - Hang'em High"] = "L307_HangemHigh.png",
            ["Crash 3 - Hog Ride"] = "L308_HogRide.png",
            ["Crash 3 - Tomb Time"] = "L309_TombTime.png",
            ["Crash 3 - Midnight Run"] = "L310_MidnightRun.png",
            ["Crash 3 - Dingodile"] = "B302_Dingodile.png",
            ["Crash 3 - Dino Might!"] = "L311_DinoMight.png",
            ["Crash 3 - Deep Trouble"] = "L312_DeepTrouble.png",
            ["Crash 3 - High Time"] = "L313_HighTime.png",
            ["Crash 3 - Road Crash"] = "L314_RoadCrash.png",
            ["Crash 3 - Double Header"] = "L315_DoubleHeader.png",
            ["Crash 3 - Dr. N. Tropy"] = "B303_NTropy.png",
            ["Crash 3 - Sphynxinator"] = "L316_Sphyynxinator.png",
            ["Crash 3 - Bye Bye Blimps"] = "L317_ByeByeBlimps.png",
            ["Crash 3 - Tell No Tales"] = "L318_TellNoTales.png",
            ["Crash 3 - Future Frenzy"] = "L319_FutureFrenzy.png",
            ["Crash 3 - Tomb Wader"] = "L320_TombWader.png",
            ["Crash 3 - Dr. N. Gin"] = "B304_NGin.png",
            ["Crash 3 - Gone Tomorrow"] = "L321_GoneTomorrow.png",
            ["Crash 3 - Orange Asphalt"] = "L322_OrangeAsphalt.png",
            ["Crash 3 - Flaming Passion"] = "L323_FlamingPassion.png",
            ["Crash 3 - Mad Bombers"] = "L324_MadBombers.png",
            ["Crash 3 - Bug Lite"] = "L325_BugLite.png",
            ["Crash 3 - Dr. Neo Cortex"] = "B305_DrNeoCortex.png",
            ["Crash 3 - Ski Crazed"] = "L326_SkiCrazed.png",
            ["Crash 3 - Area 51?"] = "L328_Area51.png",
            ["Crash 3 - Rings of Power"] = "L330_RingsOfPower.png",
            ["Crash 3 - Hot Coco"] = "L331_HotCoco.png",
            ["Crash 3 - Eggipus Rex"] = "L332_Eggipus.png",
            ["Crash 3 - Future Tense"] = "L333_FutureTense.png",
        };
        private PictureBox levelImage;
        private Control[] controlsBelowImage;
        private bool imageVisible;

        private void InitializeLevelImage(GroupBox levelBox)
        {
            controlsBelowImage = levelBox.Controls.Cast<Control>().Where(control => control.Top >= combo.Top).ToArray();
            levelImage = new PictureBox
            {
                Left = combo.Left, Top = combo.Top, Size = new Size(270, 91),
                SizeMode = PictureBoxSizeMode.Zoom, TabStop = false, Visible = false
            };
            levelBox.Controls.Add(levelImage);
            combo.SelectedIndexChanged += (s, e) => UpdateLevelImage();
            UpdateLevelImage();
            SetLevelImageVisible(true);
        }

        internal static string ImageFileForLevel(string name)
        {
            string filename;
            return name != null && LevelImages.TryGetValue(name, out filename) ? filename : "L000_None.png";
        }

        private void UpdateLevelImage()
        {
            string filename = ImageFileForLevel(combo.SelectedItem as string);
            var assembly = typeof(LevelSelectorControl).Assembly;
            using (var stream = assembly.GetManifestResourceStream("Crash.Helper.LevelImage." + filename)
                ?? assembly.GetManifestResourceStream("Crash.Helper.LevelImage.L000_None.png"))
            using (var source = Image.FromStream(stream))
            {
                var previous = levelImage.Image;
                // Detach the bitmap before closing its embedded resource stream.
                levelImage.Image = new Bitmap(source);
                previous?.Dispose();
            }
        }

        internal void SetLevelImageVisible(bool visible)
        {
            if (imageVisible == visible) return;
            var levelBox = (GroupBox)levelImage.Parent;
            int offset = levelImage.Height + (int)System.Math.Round(6.0 * levelImage.Width / 270);
            if (!visible) offset = -offset;
            SuspendLayout(); levelBox.SuspendLayout();
            imageVisible = visible;
            levelImage.Visible = visible;
            foreach (var control in controlsBelowImage) control.Top += offset;
            levelBox.Height += offset;
            levelBox.ResumeLayout(true); ResumeLayout(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && levelImage != null)
            {
                var image = levelImage.Image;
                levelImage.Image = null;
                image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
