---@diagnostic disable: undefined-global
--------------------------------------------------
-- Address & State
--------------------------------------------------
local levelAddr = '["CrashBandicootNSaneTrilogy.exe"+01A5C6D8]+20'
local currentLevel = nil
local timer = nil

--------------------------------------------------
-- Write level name with explicit NULL terminator
--------------------------------------------------
local function writeLevelName()
  if not currentLevel then return end

  -- String
  writeString(levelAddr, currentLevel, false)

  -- Level name Last NULL
  local addr = getAddress(levelAddr)
  if addr and addr ~= 0 then
    writeBytes(addr + #currentLevel, 0x00)
  end
end

--------------------------------------------------
-- Level list (Display Name -> Stage ID)
--------------------------------------------------

local LevelOrder = {
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
}

local Levels = {
  ["Crash 1 - The Wumpa Islands"]    = "crash1/l100_hub/l100_hub",
  ["Crash 1 - N. Sanity Beach"]      = "crash1/l101_nsanitybeach/l101_nsanitybeach",
  ["Crash 1 - Jungle Rollers"]       = "crash1/l102_junglerollers/l102_junglerollers",
  ["Crash 1 - The Great Gate"]       = "crash1/l103_thegreatgate/l103_thegreatgate",
  ["Crash 1 - Boulders"]             = "crash1/l104_boulders/l104_boulders",
  ["Crash 1 - Upstream"]             = "crash1/l105_upstream/l105_upstream",
  ["Crash 1 - Papu Papu"]            = "crash1/bosses/b101_papupapu/b101_papupapu",
  ["Crash 1 - Rolling Stones"]       = "crash1/l106_rollingstones/l106_rollingstones",
  ["Crash 1 - Hog Wild"]             = "crash1/l107_hogwild/l107_hogwild",
  ["Crash 1 - Native Fortress"]      = "crash1/l108_nativefortress/l108_nativefortress",
  ["Crash 1 - Up the Creek"]         = "crash1/l109_upthecreek/l109_upthecreek",
  ["Crash 1 - Ripper Roo"]           = "crash1/bosses/b102_ripperroo/b102_ripperroo",
  ["Crash 1 - The Lost City"]        = "crash1/l110_thelostcity/l110_thelostcity",
  ["Crash 1 - Temple Ruins"]         = "crash1/l111_templeruins/l111_templeruins",
  ["Crash 1 - Road to Nowhere"]      = "crash1/l112_roadtonowhere/l112_roadtonowhere",
  ["Crash 1 - Boulder Dash"]         = "crash1/l113_boulderdash/l113_boulderdash",
  ["Crash 1 - Whole Hog"]            = "crash1/l114_wholehog/l114_wholehog",
  ["Crash 1 - Sunset Vista"]         = "crash1/l115_sunsetvista/l115_sunsetvista",
  ["Crash 1 - Koala Kong"]           = "crash1/bosses/b103_koalakong/b103_koalakong",
  ["Crash 1 - Heavy Machinery"]      = "crash1/l116_heavymachinery/l116_heavymachinery",
  ["Crash 1 - Cortex Power"]         = "crash1/l117_cortexpower/l117_cortexpower",
  ["Crash 1 - Generator Room"]       = "crash1/l118_generatorroom/l118_generatorroom",
  ["Crash 1 - Toxic Waste"]          = "crash1/l119_toxicwaste/l119_toxicwaste",
  ["Crash 1 - Pinstripe Potoroo"]    = "crash1/bosses/b104_pinstripepotoroo/b104_pinstripepotoroo",
  ["Crash 1 - The High Road"]        = "crash1/l120_thehighroad/l120_thehighroad",
  ["Crash 1 - Slippery Climb"]       = "crash1/l121_slipperyclimb/l121_slipperyclimb",
  ["Crash 1 - Lights Out"]           = "crash1/l122_lightsout/l122_lightsout",
  ["Crash 1 - Fumbling in the Dark"] = "crash1/l123_fumblinginthedark/l123_fumblinginthedark",
  ["Crash 1 - Jaws of Darkness"]     = "crash1/l124_jawsofdarkness/l124_jawsofdarkness",
  ["Crash 1 - Castle Machinery"]     = "crash1/l125_castlemachinery/l125_castlemachinery",
  ["Crash 1 - Dr. Nitrus Brio"]      = "crash1/bosses/b105_drnitrusbrio/b105_drnitrusbrio",
  ["Crash 1 - The Lab"]              = "crash1/l126_thelab/l126_thelab",
  ["Crash 1 - The Great Hall"]       = "crash1/l127_thegreathall/l127_thegreathall",
  ["Crash 1 - Dr. Neo Cortex"]       = "crash1/bosses/b106_drneocortex/b106_drneocortex",
  ["Crash 1 - Stormy Ascent"]        = "crash1/l128_stormyascent/l128_stormyascent",

  ["Crash 2 - The Warp Room"]        = "crash2/l200_hub/l200_hub",
  ["Crash 2 - Turtle Woods"]         = "crash2/l201_turtlewoods/l201_turtlewoods",
  ["Crash 2 - Snow Go"]              = "crash2/l202_snowgo/l202_snowgo",
  ["Crash 2 - Hang Eight"]           = "crash2/l203_hangeight/l203_hangeight",
  ["Crash 2 - The Pits"]             = "crash2/l204_thepits/l204_thepits",
  ["Crash 2 - Crash Dash"]           = "crash2/l205_crashdash/l205_crashdash",
  ["Crash 2 - Ripper Roo"]           = "crash2/bosses/b201_ripperroo/b201_ripperroo",
  ["Crash 2 - Snow Biz"]             = "crash2/l206_snowbiz/l206_snowbiz",
  ["Crash 2 - Air Crash"]            = "crash2/l207_aircrash/l207_aircrash",
  ["Crash 2 - Bear It"]              = "crash2/l208_bearit/l208_bearit",
  ["Crash 2 - Crash Crush"]          = "crash2/l209_crashcrush/l209_crashcrush",
  ["Crash 2 - The Eel Deal"]         = "crash2/l210_theeeldeal/l210_theeeldeal",
  ["Crash 2 - Komodo Brothers"]      = "crash2/bosses/b202_komodobrothers/b202_komodobrothers",
  ["Crash 2 - Plant Food"]           = "crash2/l211_plantfood/l211_plantfood",
  ["Crash 2 - Sewer or Later"]       = "crash2/l212_sewerorlater/l212_sewerorlater",
  ["Crash 2 - Bear Down"]            = "crash2/l213_beardown/l213_beardown",
  ["Crash 2 - Road to Ruin"]         = "crash2/l214_roadtoruin/l214_roadtoruin",
  ["Crash 2 - Un-Bearable"]          = "crash2/l215_unbearable/l215_unbearable",
  ["Crash 2 - Tiny Tiger"]           = "crash2/bosses/b203_tinytiger/b203_tinytiger",
  ["Crash 2 - Hangin' Out"]          = "crash2/l216_hanginout/l216_hanginout",
  ["Crash 2 - Diggin' It"]           = "crash2/l217_digginit/l217_digginit",
  ["Crash 2 - Cold Hard Crash"]      = "crash2/l218_coldhardcrash/l218_coldhardcrash",
  ["Crash 2 - Ruination"]            = "crash2/l219_ruination/l219_ruination",
  ["Crash 2 - Bee-Having"]           = "crash2/l220_beehaving/l220_beehaving",
  ["Crash 2 - Dr. N. Gin"]           = "crash2/bosses/b204_drngin/b204_drngin",
  ["Crash 2 - Piston It Away"]       = "crash2/l221_pistonitaway/l221_pistonitaway",
  ["Crash 2 - Rock It"]              = "crash2/l222_rockit/l222_rockit",
  ["Crash 2 - Night Fight"]          = "crash2/l223_nightfight/l223_nightfight",
  ["Crash 2 - Pack Attack"]          = "crash2/l224_packattack/l224_packattack",
  ["Crash 2 - Spaced Out"]           = "crash2/l225_spacedout/l225_spacedout",
  ["Crash 2 - Dr. Neo Cortex"]       = "crash2/bosses/b205_drneocortex/b205_drneocortex",
  ["Crash 2 - Totally Bear"]         = "crash2/l226_totallybear/l226_totallybear",
  ["Crash 2 - Totally Fly"]          = "crash2/l227_totallyfly/l227_totallyfly",

  ["Crash 3 - The Time Twister"]     = "crash3/l300_hub/l300_hub",
  ["Crash 3 - Toad Village"]         = "crash3/l301_toadvillage/l301_toadvillage",
  ["Crash 3 - Under Pressure"]       = "crash3/l302_underpressure/l302_underpressure",
  ["Crash 3 - Orient Express"]       = "crash3/l303_orientexpress/l303_orientexpress",
  ["Crash 3 - Bone Yard"]            = "crash3/l304_boneyard/l304_boneyard",
  ["Crash 3 - Makin' Waves"]         = "crash3/l305_makinwaves/l305_makinwaves",
  ["Crash 3 - Tiny Tiger"]           = "crash3/bosses/b301_tinytiger/b301_tinytiger",
  ["Crash 3 - Gee Wiz"]              = "crash3/l306_geewiz/l306_geewiz",
  ["Crash 3 - Hang'em High"]         = "crash3/l307_hangemhigh/l307_hangemhigh",
  ["Crash 3 - Hog Ride"]             = "crash3/l308_hogride/l308_hogride",
  ["Crash 3 - Tomb Time"]            = "crash3/l309_tombtime/l309_tombtime",
  ["Crash 3 - Midnight Run"]         = "crash3/l310_midnightrun/l310_midnightrun",
  ["Crash 3 - Dingodile"]            = "crash3/bosses/b302_dingodile/b302_dingodile",
  ["Crash 3 - Dino Might!"]          = "crash3/l311_dinomight/l311_dinomight",
  ["Crash 3 - Deep Trouble"]         = "crash3/l312_deeptrouble/l312_deeptrouble",
  ["Crash 3 - High Time"]            = "crash3/l313_hightime/l313_hightime",
  ["Crash 3 - Road Crash"]           = "crash3/l314_roadcrash/l314_roadcrash",
  ["Crash 3 - Double Header"]        = "crash3/l315_doubleheader/l315_doubleheader",
  ["Crash 3 - Dr. N. Tropy"]         = "crash3/bosses/b303_ntropy/b303_ntropy",
  ["Crash 3 - Sphynxinator"]         = "crash3/l316_sphynxinator/l316_sphynxinator",
  ["Crash 3 - Bye Bye Blimps"]       = "crash3/l317_byebyeblimps/l317_byebyeblimps",
  ["Crash 3 - Tell No Tales"]        = "crash3/l318_tellnotales/l318_tellnotales",
  ["Crash 3 - Future Frenzy"]        = "crash3/l319_futurefrenzy/l319_futurefrenzy",
  ["Crash 3 - Tomb Wader"]           = "crash3/l320_tombwader/l320_tombwader",
  ["Crash 3 - Dr. N. Gin"]           = "crash3/bosses/b304_ngin/b304_ngin",
  ["Crash 3 - Gone Tomorrow"]        = "crash3/l321_gonetomorrow/l321_gonetomorrow",
  ["Crash 3 - Orange Asphalt"]       = "crash3/l322_orangeasphalt/l322_orangeasphalt",
  ["Crash 3 - Flaming Passion"]      = "crash3/l323_flamingpassion/l323_flamingpassion",
  ["Crash 3 - Mad Bombers"]          = "crash3/l324_madbombers/l324_madbombers",
  ["Crash 3 - Bug Lite"]             = "crash3/l325_buglite/l325_buglite",
  ["Crash 3 - Dr. Neo Cortex"]       = "crash3/bosses/b305_neocortex/b305_neocortex",
  ["Crash 3 - Ski Crazed"]           = "crash3/l326_skicrazed/l326_skicrazed",
  ["Crash 3 - Area 51?"]             = "crash3/l328_area51/l328_area51",
  ["Crash 3 - Rings of Power"]       = "crash3/l330_ringsofpower/l330_ringsofpower",
  ["Crash 3 - Hot Coco"]             = "crash3/l331_hotcoco/l331_hotcoco",
  ["Crash 3 - Eggipus Rex"]          = "crash3/l332_eggipusrex/l332_eggipusrex",
  ["Crash 3 - Future Tense"]         = "crash3/l333_futuretense/l333_futuretense"
}

--------------------------------------------------
-- UI
--------------------------------------------------
local f = createForm()
f.Caption = "CrashNST Quick Level Selector"
f.Width = 360
f.Height = 180

local cb = createComboBox(f)
cb.Left = 20
cb.Top = 20
cb.Width = 300

for _, name in ipairs(LevelOrder) do
  cb.Items.add(name)
end
cb.ItemIndex = 0


--------------------------------------------------
-- Set Level Button
--------------------------------------------------
local btn = createButton(f)
btn.Caption = "Set Level"
btn.Left = 20
btn.Top = 60
btn.Width = 120

btn.OnClick = function()
  local displayName = cb.Text
  currentLevel = Levels[displayName]
  writeLevelName()
end


--------------------------------------------------
-- Timer (Lock)
--------------------------------------------------
timer = createTimer()
timer.Interval = 1
timer.OnTimer = writeLevelName
timer.Enabled = true

--------------------------------------------------
-- Stop Button
--------------------------------------------------
local stopBtn = createButton(f)
stopBtn.Caption = "Stop Lock"
stopBtn.Left = 160
stopBtn.Top = 60
stopBtn.Width = 120

stopBtn.OnClick = function()
  currentLevel = nil
end
