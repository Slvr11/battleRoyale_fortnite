using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using System.Linq;
using InfinityScript;
using static InfinityScript.GSCFunctions;

namespace battleRoyale_fort
{
    public class br : BaseScript
    {
        public static Random rng = new Random();
        private static short fx_carePackage;
        public static short fx_smallFire;
        public static short fx_glowStickGlow;
        public static short fx_crateCollectSmoke;
        public static short fx_glow_grey;
        public static short fx_glow_green;
        public static short fx_glow_blue;
        public static short fx_glow_purple;
        public static short fx_glow_gold;
        private static bool gameHasStarted = false;
        private static bool firstCircle = true;
        private static Vector3 deployVector = Vector3.Zero;
        //private static List<Vector3> placedBuildings = new List<Vector3>();
        private static List<Entity> usables = new List<Entity>();
        private static Entity _airdropCollision;
        private static HudElem stormCounter;
        private static HudElem stormIcon;
        private static HudElem playersAlive;
        private static HudElem playersAliveIcon;
        private static readonly byte maxItemSlots = 5;
        private static int stormCircleMapID = 31;
        private static int stormCompassWidth = 300;
        private static int stormCompassHeight = 300;
        //private static Timer stormTimer = new Timer();

        private static readonly string pauseMenu = "class";

        public br()
        {
            if (GetDvar("g_gametype") != "dm")
            {
                Utilities.PrintToConsole("You must be running Battle Royale on Free-for-All!");
                SetDvar("g_gametype", "dm");
                Utilities.ExecuteCommand("map_restart");
                return;
            }
            if (GetDvarInt("sv_maxclients") < 18)
            {
                SetDvar("sv_maxclients", 18);
                Utilities.ExecuteCommand("map_restart");
                return;
            }

            _airdropCollision = GetEnt("care_package", "targetname");
            if (_airdropCollision != null) _airdropCollision = GetEnt(_airdropCollision.Target, "targetname");

            precacheGametype();

            //Utilities.SetDropItemEnabled(false);

            MakeDvarServerInfo("ui_gametype", "Battle Royale");
            MakeDvarServerInfo("sv_gametypeName", "Battle Royale");
            MakeDvarServerInfo("camera_thirdperson", 1);
            //-Enable turning anims on players-
            SetDvar("player_turnAnims", 1);
            //Set high quality voice chat audio
            SetDvar("sv_voiceQuality", 9);
            SetDvar("maxVoicePacketsPerSec", 1000);
            SetDvar("maxVoicePacketsPerSecForServer", 200);
            //Ensure all players are heard regardless of any settings
            SetDvar("cg_everyoneHearsEveryone", 1);
            SetDvar("scr_game_playerwaittime", 50);
            SetDvar("scr_game_matchstarttime", 10);
            SetDvar("ui_hud_showdeathicons", "0");//Disable death icons
            AfterDelay(100, () => MakeDvarServerInfo("camera_thirdperson", 1));
            SetDvar("camera_thirdPersonCrosshairOffset", -0.05f);
            SetDvar("camera_thirdPersonOffset", new Vector3(-120, -14, 8));
            AfterDelay(50, () => SetDynamicDvar("scr_player_healthregentime", 0));

            SetTeamRadar("none", true);

            PlayerConnected += onPlayerConnect;
            Notified += onGlobalNotify;

            createServerHud();

            initDeployVectors();

            //stormTimer.Elapsed += (a, b) => initNextStormCircle();//Init once
        }

        private static void precacheGametype()
        {
            //load fx
            fx_carePackage = (short)LoadFX("smoke/signal_smoke_airdrop");
            fx_smallFire = (short)LoadFX("fire/vehicle_exp_fire_spwn_child");
            fx_glowStickGlow = (short)LoadFX("misc/glow_stick_glow_green");
            fx_glow_gold = (short)LoadFX("misc/outdoor_motion_light");
            fx_glow_grey = (short)LoadFX("props/glow_latern");
            fx_glow_green = (short)LoadFX("misc/glow_stick_glow_green");
            fx_glow_blue = (short)LoadFX("misc/aircraft_light_cockpit_blue");
            fx_glow_purple = (short)LoadFX("misc/glow_stick_glow_red");
            fx_crateCollectSmoke = (short)LoadFX("props/crateexp_dust");

            //PreCacheItem("at4_mp");
            //PreCacheItem("iw5_mk12spr_mp");
            PreCacheItem("lightstick_mp");
            //PreCacheItem("iw5_xm25_mp");
            PreCacheShader("line_horizontal");
            PreCacheShader("hud_iw5_divider");
            PreCacheShader("deaths_skull");
            PreCacheShader("cardicon_radiation");
            PreCacheShader("killiconheadshot");
            PreCacheShader("viper_ammo_overlay_mp");
            PreCacheShader("gradient_center");
            PreCacheShader("hud_killstreak_frame");
            PreCacheShader("weapon_missing_image");
            PreCacheShader("progress_bar_fill");
            PreCacheShader("weapon_m4_short");
            PreCacheShader("unlock_camo_temp");
            PreCacheHeadIcon("waypoint_revive");
            PreCacheStatusIcon("cardicon_iwlogo");
            PreCacheMiniMapIcon("compassping_portable_radar_sweep");
            PreCacheShader("compassping_portable_radar_sweep");
        }

        private static void onPlayerConnect(Entity player)
        {
            //-Player netcode-
            player.SetClientDvars("snaps", 30, "rate", 30000);
            player.SetClientDvar("cl_maxPackets", 100);
            player.SetClientDvar("cl_packetdup", 0);
            //-End player netcode-

            //Disable RCon for clients because sad day
            player.SetClientDvar("cl_enableRCon", 0);

            player.SetField("isViewingScoreboard", false);
            refreshScoreboard(player);

            player.SetClientDvar("g_hardcore", "1");
            player.SetClientDvar("cg_scoreboardWidth", 750);
            player.SetClientDvar("cg_thirdperson", 0);
            player.SetClientDvar("camera_thirdperson", 1);
            player.SetClientDvar("camera_thirdPersonCrosshairOffset", -0.05f);
            player.SetClientDvar("camera_thirdPersonOffset", new Vector3(-120, -14, 8));
            player.SetClientDvar("scr_player_healthregentime", 0);
            player.SetViewKickScale(0.1f);
            player.SetClientDvars("bg_legYawTolerance", 50, "player_turnAnims", 1);
            player.SetField("lastDroppableWeapon", "none");
            player.SetField("weaponsList", new Parameter(new string[6] {"", "", "", "", "", ""}));
            player.SetField("currentlySelectedWeaponSlot", 0);
            player.SetField("isInBuildMode", false);
            player.NotifyOnPlayerCommand("toggleBuilding", "+frag");
            player.NotifyOnPlayerCommand("nextWeapon", "weapnext");
            player.NotifyOnPlayerCommand("prevWeapon", "weapprev");

            //Reset certain dvars that some servers may have set and not restored
            player.SetClientDvar("waypointIconHeight", "36");
            player.SetClientDvar("waypointIconWidth", "36");

            player.CloseInGameMenu();
            if (!gameHasStarted)
                spawnPlayer(player);
            player.SetClientDvar("g_scriptMainMenu", pauseMenu);

            createPlayerHud(player);

            player.SpawnedPlayer += () => onPlayerSpawn(player);
        }

        private static void spawnPlayer(Entity player)
        {
            Entity randomSpawn = getRandomSpawnpoint();
            player.Spawn(randomSpawn.Origin, randomSpawn.Angles);
            player.SessionState = "playing";
            player.SessionTeam = "none";
            player.MaxHealth = 100;
            player.Health = 100;
            player.TakeAllWeapons();
            player.ClearPerks();
            player.Notify("spawned_player");
        }

        private static void onPlayerSpawn(Entity player)
        {
            setSpawnModel(player);
            player.SetClientDvars("g_hardcore", "1", "cg_drawCrosshair", "1", "ui_drawCrosshair", "1");
            player.SetClientDvar("cg_objectiveText", "Be the last man standing");
            player.SetClientDvar("g_scriptMainMenu", pauseMenu);
            player.SetClientDvar("cg_thirdperson", 0);
            player.SetClientDvar("camera_thirdperson", 1);
            player.SetClientDvar("compassRotation", false);
            player.OpenMenu("perk_hide");
            player.CloseMenu("team_marinesopfor");
            giveStartingWeapon(player);
            player.DisableWeaponPickup();
            player.DisableWeaponSwitch();
            player.SetClientDvar("camera_thirdPersonCrosshairOffset", -0.05f);
            player.SetClientDvar("camera_thirdPersonOffset", new Vector3(-120, -14, 8));
            player.SetPerk("specialty_marathon", true, true);

            if (checkPlayerDev(player))
            {
                player.StatusIcon = "cardicon_iwlogo";
                player.Name = "^2Slvr99^7";
                player.SetField("isDev", true);
            }

            updateWeaponsHud(player);
            updatePlayerCountForScoreboard();
            updatePlayersAliveCount();

            player.SetEMPJammed(false);
        }

        public override void OnSay(Entity player, string name, string message)
        {
            if (message.StartsWith("drop "))
            {
                int slot;
                if (int.TryParse(message.Split(' ')[1], out slot))
                {
                    dropWeapon(player, slot);
                }
            }
            if (message == "dropAll")
            {
                dropAllPlayerWeapons(player);
            }
            else if (message.StartsWith("-give "))
            {
                string weapon = message.Split(' ')[1];
                int variant = 0;
                if (message.Split(' ').Length > 2)
                {
                    variant = int.Parse(message.Split(' ')[2]);
                }

                string variantText = variant > 0 ? "_camo0" + variant : "";
                giveWeapon(player, weapon + variantText, WeaponClipSize(weapon), WeaponClipSize(weapon));
            }
        }

        public override void OnPlayerDisconnect(Entity player)
        {
            destroyPlayerHud(player);
            updatePlayerCountForScoreboard();
            updatePlayersAliveCount();
        }

        private static void initDeployVectors()
        {
            Vector3 center = getMapCenter();
            center = center.Around(200);
            center.Z = 5000;//Get height for map
            deployVector = center;
        }
        private static Vector3 getMapCenter()
        {
            Vector3 ret = Vector3.Zero;

            Entity centerEnt = GetEnt("sab_bomb", "targetname");
            if (centerEnt != null)
                ret = centerEnt.Origin;

            return ret;
        }
        private static void setNextStormPosition()
        {
            //Lower bounds by 50
        }
        private static void startStormCountdownTimer(float time)
        {
            stormCounter.SetTimer(time);
            stormCounter.Color = new Vector3(1, 1, 1);
            //stormIcon.SetText("Storm moving in:");
        }
        private static void startStormTimer(float time)
        {
            //stormTimer.Interval = time * 1000;
            //stormTimer.Enabled = true;
            //stormTimer.Start();
            stormCounter.SetTimer(time);
            stormCounter.Color = new Vector3(.4f, .1f, .9f);
            OnInterval((stormCompassHeight) * 1000, stormTimer_update);
        }
        private static bool stormTimer_update()
        {
            stormCompassHeight -= 1;
            stormCompassWidth -= 1;
            foreach (Entity players in Players)
            {
                if (!players.IsPlayer) continue;

                players.SetClientDvars("compassObjectiveIconHeight", stormCompassHeight, "compassObjectiveIconWidth", stormCompassWidth);
                players.SetClientDvars("compassObjectiveHeight", stormCompassHeight / 1.66f, "compassObjectiveWidth", stormCompassWidth / 1.66f);
            }
            //if (!stormTimer.Enabled) return false;
            return true;
        }
        private static void initNextStormCircle()
        {
            if (firstCircle) return;
            InfinityScript.Log.Debug("Storm circle completed!");
            //stormTimer.Enabled = false;
            //stormTimer.Stop();
            //stormTimer.Interval *= 0.8f;
            setNextStormPosition();
        }

        private static void onGlobalNotify(int entRef, string message, params Parameter[] parameters)
        {
            if (message == "prematch_over")
            {
                startStormCountdownTimer(60);
                //AfterDelay(60000, () => Notify("start_circle"));
            }
            if (message == "start_circle")
            {
                //initDeployVectors();
                if (deployVector.Equals(Vector3.Zero))
                {
                    Utilities.PrintToConsole("deployVector was not set up when circle was started!");
                    return;
                }

                Objective_Add(stormCircleMapID, "active", deployVector, "compassping_portable_radar_sweep");
                foreach (Entity players in Players)
                {
                    if (!players.IsPlayer) continue;

                    players.SetClientDvars("compassObjectiveIconHeight", stormCompassHeight, "compassObjectiveIconWidth", stormCompassWidth);
                }
                startStormTimer(180);
            }

            if (entRef > 2046) return;
            Entity player = Entity.GetEntity(entRef);

            if (message == "reload")
            {
                updateAmmoHud(player);
            }

            else if (message == "weapon_switch_started" || message == "weapon_change")
            {
                updateWeaponsHud(player);
                updateAmmoHud(player);
            }

            else if (message == "weapon_fired")
            {
                updateAmmoHud(player);
            }
            /*
            else if (message == "toggleBuilding")
            {
                if (!player.GetField<bool>("isInBuildMode"))
                {
                    enterBuildingMode(player);
                }
                else exitBuildingMode(player);
            }
            */
            else if (message == "nextWeapon")
            {
                if (player.GetField<bool>("isInBuildMode"))
                {
                    return;
                }
                int currentWeapon = player.GetField<int>("currentlySelectedWeaponSlot");
                string[] weaponList = player.GetField<string[]>("weaponsList");
                currentWeapon++;
                if (player.CurrentWeapon == weaponList[currentWeapon]) return;

                if (currentWeapon > maxItemSlots - 1) currentWeapon = 0;
                while (weaponList[currentWeapon] == "")
                {
                    currentWeapon++;
                    if (currentWeapon > maxItemSlots - 1)
                    {
                        currentWeapon = 0;
                        break;
                    }
                }
                player.SetField("currentlySelectedWeaponSlot", currentWeapon);
                player.SwitchToWeaponImmediate(weaponList[currentWeapon]);
                updateWeaponsHud(player);
            }
            else if (message == "prevWeapon")
            {
                if (player.GetField<bool>("isInBuildMode"))
                {
                    return;
                }
                int currentWeapon = player.GetField<int>("currentlySelectedWeaponSlot");
                string[] weaponList = player.GetField<string[]>("weaponsList");
                currentWeapon--;
                if (player.CurrentWeapon == weaponList[currentWeapon]) return;

                if (currentWeapon < 0) currentWeapon = 5;
                while (weaponList[currentWeapon] == "")
                {
                    currentWeapon--;
                    if (currentWeapon < 0)
                    {
                        currentWeapon = 0;
                        break;
                    }
                }
                player.SetField("currentlySelectedWeaponSlot", currentWeapon);
                player.SwitchToWeaponImmediate(weaponList[currentWeapon]);
                updateWeaponsHud(player);
            }
            else if (message == "toggleBuilding")
            {
                if (player.GetField<bool>("isInBuildMode"))
                    exitBuildingMode(player);

                else enterBuildingMode(player);
            }
        }
        
        private static void enterBuildingMode(Entity player)
        {
            player.SetField("isInBuildMode", true);
            player.GiveWeapon("c4_mp");
            player.SetWeaponAmmoClip("c4_mp", 0);
            player.SetWeaponAmmoStock("c4_mp", 0);
            player.SwitchToWeaponImmediate("c4_mp");
            Entity building = spawnBuildMarker(player);
            player.SetField("building", building);
            OnInterval(50, () => watchBuildMode(player));
        }
        private static Entity spawnBuildMarker(Entity player)
        {
            Entity origin = Spawn("script_model", Vector3.Zero);
            origin.SetModel("tag_origin");

            List<Entity> pieces = new List<Entity>();
            for (int i = 0; i < 6; i++)
            {
                Entity crate = Spawn("script_model", origin.Origin);
                crate.SetModel("com_plasticcase_trap_bombsquad");
                crate.Hide();
                crate.ShowToPlayer(player);
                Vector3 offset = Vector3.Zero;
                switch (i)
                {
                    case 0:
                        offset = new Vector3(28, 30, -45);
                        break;
                    case 1:
                        offset = new Vector3(-28, 30, -45);
                        break;
                    case 2:
                        offset = new Vector3(28, -30, -45);
                        break;
                    case 3:
                        offset = new Vector3(-28, -30, -45);
                        break;
                    case 4:
                        offset = new Vector3(28, 0, -45);
                        break;
                    case 5:
                        offset = new Vector3(-28, 0, -45);
                        break;
                }
                crate.LinkTo(origin, "tag_origin", offset);
                pieces.Add(crate);
            }
            origin.SetField("pieces", new Parameter(pieces));
            return origin;
        }
        private static void exitBuildingMode(Entity player)
        {
            player.GetField<Entity>("building").GetField<List<Entity>>("pieces").ForEach((p) => p.Delete());
            player.GetField<Entity>("building").Delete();
            player.ClearField("building");
            player.SetField("isInBuildMode", false);
            player.SwitchToWeaponImmediate(player.GetField<string[]>("weaponsList")[player.GetField<int>("currentlySelectedWeaponSlot")]);
        }
        private static Entity spawnBuilding(Vector3 location, Vector3 angles, bool pyramid)
        {
            Entity origin = Spawn("script_model", Vector3.Zero);
            origin.SetModel("tag_origin");
            origin.Hide();

            if (!pyramid)
            {
                List<Entity> pieces = new List<Entity>();
                for (int i = 0; i < 6; i++)
                {
                    Entity crate = Spawn("script_model", origin.Origin);
                    crate.SetModel("com_plasticcase_enemy");
                    crate.CloneBrushModelToScriptModel(_airdropCollision);
                    Vector3 offset = Vector3.Zero;
                    switch (i)
                    {
                        case 0:
                            offset = new Vector3(35, 35, -45);
                            break;
                        case 1:
                            offset = new Vector3(-35, 35, -45);
                            break;
                        case 2:
                            offset = new Vector3(35, -35, -45);
                            break;
                        case 3:
                            offset = new Vector3(-35, -35, -45);
                            break;
                        case 4:
                            offset = new Vector3(35, 0, -45);
                            break;
                        case 5:
                            offset = new Vector3(-35, 0, -45);
                            break;
                    }
                    crate.LinkTo(origin, "tag_origin", offset);
                    pieces.Add(crate);
                }
                origin.SetField("pieces", new Parameter(pieces));
                origin.Origin = location;
                origin.Angles = angles;
            }
            //placedBuildings.Add(origin.Origin);
            return origin;
        }
        private static bool watchBuildMode(Entity player)
        {
            if (!player.IsAlive || !player.GetField<bool>("isInBuildMode")) return false;
            if (player.CurrentWeapon != "c4_mp")
            {
                if (!player.HasWeapon("c4_mp"))
                {
                    player.GiveWeapon("c4_mp");
                    player.SetWeaponAmmoClip("c4_mp", 0);
                    player.SetWeaponAmmoStock("c4_mp", 0);
                    player.SwitchToWeaponImmediate("c4_mp");
                }
                else player.SwitchToWeaponImmediate("c4_mp");
                return true;
            }

            Vector3 angleToForward = AnglesToForward(player.GetPlayerAngles());
            Vector3 viewLocation = player.GetEye() + angleToForward * 150;
            Vector3 buildingLocation = getBuildingGridLocation(viewLocation);
            Vector3 playerAngles = player.GetPlayerAngles();

            int yTweak = -90;
            if (playerAngles.Y < 45) yTweak = -180;
            //90 * (int)((playerAngles.Z - 45) / 90) + yTweak
            Vector3 angles = new Vector3(45/*(45 if ramp, 0 if floor, 90 if wall)*/, 90 * (int)((playerAngles.Y - 45) / 90) + yTweak, 0);

            if (player.HasField("building"))
            {
                Entity building = player.GetField<Entity>("building");
                building.Origin = buildingLocation;
                building.Angles = angles;

                if (player.AttackButtonPressed())
                {
                    spawnBuilding(building.Origin, building.Angles, false);
                }
            }

            return true;
        }
        private static bool canPlaceBuilding(Vector3 location)//Add angle check
        {
            bool ret = true;

            //if (placedBuildings.Contains(location)) ret = false;

            return ret;
        }
        private static Vector3 getBuildingGridLocation(Vector3 location)
        {
            Vector3 ret = Vector3.Zero;
            int gridSeperator = 100;
            ret = new Vector3(gridSeperator * (int)(location.X / gridSeperator), gridSeperator * (int)(location.Y / gridSeperator), gridSeperator * (int)(location.Z / gridSeperator));
            int xTweak = 50;
            if (location.X < 0) xTweak = -50;
            int yTweak = 50;
            if (location.Y < 0) yTweak = -50;
            int zTweak = 50;
            if (location.Z < 0) zTweak = -50;
            ret += new Vector3(xTweak, yTweak, zTweak);
            return ret;
        }

        private static void giveStartingWeapon(Entity player)
        {
            giveWeapon(player, "defaultweapon_mp", 0, 0, 5);//Set custom playerAnimType in memory if a better one exists
            //giveWeapon(player, "iw5_m4_mp_camo03", 30, 0, 4);
        }

        private static void setSpawnModel(Entity player)
        {
            player.SetModel(getPlayerModelsForLevel(false));
            //player.SetViewModel(bodyModel);
            player.Attach(getPlayerModelsForLevel(true), "j_spine4", true);
            player.ShowPart("j_spine4", getPlayerModelsForLevel(true));
            //player.Show();
        }

        public override void OnPlayerKilled(Entity player, Entity inflictor, Entity attacker, int damage, string mod, string weapon, Vector3 dir, string hitLoc)
        {
            AfterDelay(0, () =>
            {
                updatePlayerCountForScoreboard();
                dropAllPlayerWeapons(player);
                clearPlayerWeaponsList(player);

                player.IPrintLnBold(string.Format("You placed {0}!", getPlayerRank(player)));

                updatePlayersAliveCount();
            });
        }
        public override void OnPlayerDamage(Entity player, Entity inflictor, Entity attacker, int damage, int dFlags, string mod, string weapon, Vector3 point, Vector3 dir, string hitLoc)
        {
            AfterDelay(50, () => updatePlayerHealthHud(player));
        }

        public static int getPlayerRank(Entity player)
        {
            int rank = 0;
            foreach (Entity players in Players)
            {
                if (players == player) continue;
                if (players.IsAlive)
                    rank++;
            }
            return rank;
        }
        private static Entity getRandomSpawnpoint()
        {
            Entity ret = null;
            for (int i = 0; i < 700; i++)
            {
                Entity e = Entity.GetEntity(i);
                if (e == null) continue;
                if (e.Classname == "mp_dm_spawn")
                {
                    ret = e;
                    if (rng.Next(100) > 50) break;
                }
                else continue;
            }
            return ret;
        }

        public static void createServerHud()
        {
            stormCounter = NewHudElem();
            stormCounter.X = 25;
            stormCounter.Y = 120;
            stormCounter.AlignX = HudElem.XAlignments.Left;
            stormCounter.AlignY = HudElem.YAlignments.Top;
            stormCounter.HorzAlign = HudElem.HorzAlignments.Left_Adjustable;
            stormCounter.VertAlign = HudElem.VertAlignments.Top_Adjustable;
            stormCounter.Foreground = true;
            stormCounter.Alpha = 1;
            stormCounter.Archived = true;
            stormCounter.HideWhenInMenu = true;
            stormCounter.Font = HudElem.Fonts.Default;
            stormCounter.FontScale = 1.5f;

            stormIcon = NewHudElem();
            stormIcon.X = 5;
            stormIcon.Y = 120;
            stormIcon.AlignX = HudElem.XAlignments.Left;
            stormIcon.AlignY = HudElem.YAlignments.Top;
            stormIcon.HorzAlign = HudElem.HorzAlignments.Left_Adjustable;
            stormIcon.VertAlign = HudElem.VertAlignments.Top_Adjustable;
            stormIcon.Foreground = true;
            stormIcon.Alpha = 1;
            stormIcon.Archived = true;
            stormIcon.HideWhenInMenu = true;
            stormIcon.SetShader("cardicon_radiation", 16, 16);

            playersAlive = NewHudElem();
            playersAlive.X = 80;
            playersAlive.Y = 120;
            playersAlive.AlignX = HudElem.XAlignments.Left;
            playersAlive.AlignY = HudElem.YAlignments.Top;
            playersAlive.HorzAlign = HudElem.HorzAlignments.Left_Adjustable;
            playersAlive.VertAlign = HudElem.VertAlignments.Top_Adjustable;
            playersAlive.Foreground = true;
            playersAlive.Alpha = 1;
            playersAlive.Archived = true;
            playersAlive.HideWhenInMenu = true;
            playersAlive.Font = HudElem.Fonts.Default;
            playersAlive.FontScale = 1.5f;
            playersAlive.SetValue(0);

            playersAliveIcon = NewHudElem();
            playersAliveIcon.X = 60;
            playersAliveIcon.Y = 120;
            playersAliveIcon.AlignX = HudElem.XAlignments.Left;
            playersAliveIcon.AlignY = HudElem.YAlignments.Top;
            playersAliveIcon.HorzAlign = HudElem.HorzAlignments.Left_Adjustable;
            playersAliveIcon.VertAlign = HudElem.VertAlignments.Top_Adjustable;
            playersAliveIcon.Foreground = true;
            playersAliveIcon.Alpha = 1;
            playersAliveIcon.Archived = true;
            playersAliveIcon.HideWhenInMenu = true;
            playersAliveIcon.SetShader("killiconheadshot", 16, 16);
        }

        public static void createPlayerHud(Entity player)
        {
            if (player.HasField("hud_created")) return;

            //Ammo counters
            HudElem ammoSlash = HudElem.CreateFontString(player, HudElem.Fonts.Bold, 2f);
            ammoSlash.SetPoint("bottom", "bottom", 0, -100);
            ammoSlash.HideWhenInMenu = true;
            ammoSlash.HideWhenDead = true;
            ammoSlash.Archived = true;
            ammoSlash.LowResBackground = true;
            ammoSlash.Alpha = .4f;
            ammoSlash.SetText("|");
            ammoSlash.Sort = 0;

            HudElem ammoStock = HudElem.CreateFontString(player, HudElem.Fonts.Bold, 2f);
            ammoStock.Parent = ammoSlash;
            ammoStock.SetPoint("left", "left", 8);
            ammoStock.HideWhenInMenu = true;
            ammoStock.HideWhenDead = true;
            ammoStock.Archived = true;
            ammoStock.SetValue(0);
            ammoStock.Sort = 0;

            HudElem ammoClip = HudElem.CreateFontString(player, HudElem.Fonts.Bold, 2f);
            ammoClip.Parent = ammoSlash;
            ammoClip.SetPoint("right", "right", -8);
            ammoClip.HideWhenInMenu = true;
            ammoClip.HideWhenDead = true;
            ammoClip.Archived = true;
            ammoClip.SetValue(0);
            ammoClip.Sort = 0;

            HudElem ammoIcon = HudElem.CreateIcon(player, "viper_ammo_overlay_mp", 16, 16);
            ammoIcon.Parent = ammoStock;
            ammoIcon.SetPoint("left", "left", 12);
            ammoIcon.HideWhenInMenu = true;
            ammoIcon.HideWhenDead = true;
            ammoIcon.Archived = true;
            ammoIcon.Sort = 0;

            //Set player fields for ammo hud
            player.SetField("hud_ammoSlash", ammoSlash);
            player.SetField("hud_ammoStock", ammoStock);
            player.SetField("hud_ammoClip", ammoClip);
            player.SetField("hud_ammoIcon", ammoIcon);

            //Item boxes
            for (int i = 0; i < maxItemSlots; i++)
            {
                HudElem itemBox = NewClientHudElem(player);
                itemBox.HorzAlign = HudElem.HorzAlignments.Right_Adjustable;
                itemBox.VertAlign = HudElem.VertAlignments.Bottom_Adjustable;
                itemBox.X = -100 - (i * 40);
                itemBox.Y = -100;
                itemBox.Alpha = 1;
                itemBox.Archived = true;
                itemBox.HideWhenDead = true;
                itemBox.LowResBackground = true;
                itemBox.SetShader("hud_killstreak_frame", 32, 32);//hud_killstreak_frame
                itemBox.Sort = 2;
                player.SetField("hud_itemBox" + i, itemBox);

                HudElem item = NewClientHudElem(player);
                item.Parent = itemBox;
                item.SetPoint("center", "center", -12);
                item.Alpha = 1;
                item.Archived = true;
                item.HideWhenDead = true;
                item.Foreground = true;
                item.LowResBackground = true;
                item.SetShader("weapon_missing_image", 28, 32);
                item.Sort = 3;
                itemBox.SetField("itemIcon", item);

                //if (i == 5) continue;
                HudElem itemCount = HudElem.CreateFontString(player, HudElem.Fonts.Default, .7f);
                itemCount.Parent = itemBox;
                itemCount.SetPoint("bottom right", "bottom right");
                itemCount.Alpha = 0;
                itemCount.Archived = true;
                itemCount.HideWhenDead = true;
                itemCount.Foreground = true;
                itemCount.LowResBackground = true;
                itemCount.SetValue(0);
                itemCount.Sort = 3;
                item.SetField("itemCount", itemCount);
            }

            //Hitmarker
            HudElem hitFeedback = NewClientHudElem(player);
            hitFeedback.HorzAlign = HudElem.HorzAlignments.Center;
            hitFeedback.VertAlign = HudElem.VertAlignments.Middle;
            hitFeedback.X = -12;
            hitFeedback.Y = -12;
            hitFeedback.Alpha = 0;
            hitFeedback.Archived = true;
            hitFeedback.HideWhenDead = false;
            hitFeedback.SetShader("damage_feedback", 24, 48);
            hitFeedback.Sort = 2;
            hitFeedback.Color = new Vector3(.5f, 0, 0);
            player.SetField("hud_damageFeedback", hitFeedback);

            //health hud
            HudElem health = HudElem.CreateIcon(player, "progress_bar_fill", 100, 12);
            health.SetPoint("bottom left", "bottom left", 377, -70);
            health.HideWhenInMenu = true;
            health.Foreground = false;
            health.Archived = true;
            health.Alpha = 1;
            health.Color = new Vector3(0, 0.7f, 0);
            health.Sort = 10;
            /*
            HudElem healthBG = HudElem.CreateIcon(player, "progress_bar_fill", 100, 10);
            healthBG.Parent = health;
            healthBG.SetPoint("center");
            healthBG.HideWhenInMenu = true;
            healthBG.Foreground = false;
            healthBG.Archived = true;
            healthBG.Alpha = .7f;
            healthBG.Color = new Vector3(0, 0, 0);
            healthBG.Sort = 10;
            */
            HudElem healthNumber = HudElem.CreateFontString(player, HudElem.Fonts.Default, 1);
            healthNumber.Parent = health;
            healthNumber.SetPoint("left", "left", 15);
            healthNumber.HideWhenInMenu = true;
            healthNumber.Foreground = true;
            healthNumber.Archived = true;
            healthNumber.Alpha = 1;
            healthNumber.SetValue(Math.Min(player.Health, 100));
            healthNumber.Sort = 10;
            HudElem healthMax = HudElem.CreateFontString(player, HudElem.Fonts.Default, 1);
            healthMax.Parent = healthNumber;
            healthMax.SetPoint("left", "left", 16);
            healthMax.HideWhenInMenu = true;
            healthMax.Foreground = true;
            healthMax.Archived = true;
            healthMax.Alpha = .6f;
            healthMax.SetText("| 100");
            healthMax.Sort = 10;
            health.SetField("percent", Math.Min(player.Health, 100));

            HudElem shield = HudElem.CreateIcon(player, "progress_bar_fill", 100, 12);
            shield.SetPoint("bottom left", "bottom left", 377, -84);
            shield.HideWhenInMenu = true;
            shield.Foreground = false;
            shield.Archived = true;
            shield.Alpha = 1;
            shield.Color = new Vector3(0, 0.4f, 0.7f);
            shield.Sort = 10;
            /*
            HudElem shieldBG = HudElem.CreateIcon(player, "progress_bar_fill", 100, 10);
            shieldBG.Parent = shield;
            shieldBG.SetPoint("center");
            shieldBG.HideWhenInMenu = true;
            shieldBG.Foreground = false;
            shieldBG.Archived = true;
            shieldBG.Alpha = .7f;
            shieldBG.Color = new Vector3(0, 0, 0);
            shieldBG.Sort = 10;
            */
            HudElem shieldNumber = HudElem.CreateFontString(player, HudElem.Fonts.Default, 1);
            shieldNumber.Parent = shield;
            shieldNumber.SetPoint("left", "LEFT", 15);
            shieldNumber.HideWhenInMenu = true;
            shieldNumber.Foreground = true;
            shieldNumber.Archived = true;
            shieldNumber.Alpha = 1;
            shieldNumber.SetValue(Math.Max(player.Health - 100, 0));
            shieldNumber.Sort = 10;
            HudElem shieldMax = HudElem.CreateFontString(player, HudElem.Fonts.Default, 1);
            shieldMax.Parent = shieldNumber;
            shieldMax.SetPoint("left", "left", 16);
            shieldMax.HideWhenInMenu = true;
            shieldMax.Foreground = true;
            shieldMax.Archived = true;
            shieldMax.Alpha = .6f;
            shieldMax.SetText("| 100");
            shieldMax.Sort = 10;
            shield.SetField("percent", Math.Max(player.Health - 100, 0));

            player.SetField("hud_health", health);
            player.SetField("hud_healthNumber", healthNumber);
            player.SetField("hud_shield", shield);
            player.SetField("hud_shieldNumber", shieldNumber);

            //usables message
            HudElem message = HudElem.CreateFontString(player, HudElem.Fonts.Default, 1.6f);
            message.SetPoint("CENTER", "CENTER", 0, 110);
            message.HideWhenInMenu = true;
            message.HideWhenDead = true;
            //message.Foreground = true;
            message.Alpha = 0;
            message.Archived = true;
            message.Sort = 20;
            player.SetField("hud_message", message);

            player.SetField("hud_created", true);

            //Update our ammo counters
            updateAmmoHud(player);
        }
        public static void destroyPlayerHud(Entity player)
        {
            if (!player.HasField("hud_created")) return;
            HudElem[] HUD = new HudElem[10] {
                player.GetField<HudElem>("hud_ammoSlash"),
                player.GetField<HudElem>("hud_ammoStock"),
                player.GetField<HudElem>("hud_ammoClip"),
                player.GetField<HudElem>("hud_ammoIcon"),
                player.GetField<HudElem>("hud_health"),
                player.GetField<HudElem>("hud_healthNumber"),
                player.GetField<HudElem>("hud_shield"),
                player.GetField<HudElem>("hud_shieldNumber"),
                player.GetField<HudElem>("hud_damageFeedback"),
                player.GetField<HudElem>("hud_message") };

            HUD[4].Children.ForEach((h) => h.Destroy());
            HUD[5].Children.ForEach((h) => h.Destroy());
            HUD[6].Children.ForEach((h) => h.Destroy());
            HUD[7].Children.ForEach((h) => h.Destroy());

            foreach (HudElem hud in HUD)
            {
                //hud.Reset();
                if (hud == null) continue;
                hud.Destroy();
            }

            for (int i = 0; i < maxItemSlots; i++)
            {
                HudElem itemBox = player.GetField<HudElem>("hud_itemBox" + i);
                itemBox.Children.ForEach((h) => h.Destroy());
                itemBox.Destroy();
            }

            player.ClearField("hud_ammoSlash");
            player.ClearField("hud_ammoStock");
            player.ClearField("hud_ammoClip");
            player.ClearField("hud_ammoIcon");
            player.ClearField("hud_health");
            player.ClearField("hud_healthNumber");
            player.ClearField("hud_shield");
            player.ClearField("hud_shieldNumber");
            player.ClearField("hud_damageFeedback");
            player.ClearField("hud_message");
            player.ClearField("hud_created");
        }
        public static void updateWeaponsHud(Entity player)
        {
            if (!player.HasField("hud_created") || (player.HasField("hud_created") && !player.GetField<bool>("hud_created")))
                return;

            //Update all slots
            for (int i = 0; i < maxItemSlots; i++)
            {
                string[] weaponList = player.GetField<string[]>("weaponsList");
                if (weaponList[i] == "")
                {
                    player.GetField<HudElem>("hud_itemBox" + i).SetShader("hud_killstreak_frame", 32, 32);
                    player.GetField<HudElem>("hud_itemBox" + i).GetField("itemIcon").As<HudElem>().SetShader("weapon_missing_image", 24, 24);
                    try { player.GetField<HudElem>("hud_itemBox" + i).GetField("itemIcon").As<HudElem>().GetField("itemCount").As<HudElem>().Alpha = 0; }
                    catch { continue; }
                    continue;
                }

                HudElem itemSlot = player.GetField<HudElem>("hud_itemBox" + i);
                itemSlot.Color = getWeaponRarityColor(weaponList[i]);
                if (player.GetField<int>("currentlySelectedWeaponSlot") == i)
                {
                    itemSlot.SetShader("gradient_center", itemSlot.Width, itemSlot.Height);
                    itemSlot.ScaleOverTime(.3f, 38, 38);
                    itemSlot.Width = 38;
                    itemSlot.Height = 38;
                }
                else
                {
                    itemSlot.SetShader("gradient_center", itemSlot.Width, itemSlot.Height);
                    itemSlot.ScaleOverTime(.3f, 32, 32);
                    itemSlot.Width = 32;
                    itemSlot.Height = 32;
                }
                itemSlot = (HudElem)itemSlot.GetField("itemIcon");
                itemSlot.SetShader(getWeaponIcon(weaponList[i]), 24, 24);
                try//Using try/catch due to no HasField for Hud
                {
                    itemSlot = (HudElem)itemSlot.GetField("itemCount");
                    itemSlot.SetValue(player.GetAmmoCount(weaponList[i]));
                    itemSlot.Alpha = 1;
                }
                catch
                { continue; }
            }
        }
        public static void updateAmmoHud(Entity player)
        {
            if (!player.HasField("hud_created") || (player.HasField("hud_created") && !player.GetField<bool>("hud_created")))
                return;

            HudElem ammoStock = player.GetField<HudElem>("hud_ammoStock");
            HudElem ammoClip = player.GetField<HudElem>("hud_ammoClip");
            HudElem ammoSlash = player.GetField<HudElem>("hud_ammoSlash");
            HudElem ammoIcon = player.GetField<HudElem>("hud_ammoIcon");
            string weapon = player.CurrentWeapon;

            if (weapon == "none" || weapon == "c4_mp" || weapon == "c4death_mp" || weapon == "defaultweapon_mp")
            {
                ammoStock.Alpha = 0;
                ammoClip.Alpha = 0;
                ammoIcon.Alpha = 0;
                ammoSlash.SetText("");
            }
            else
            {
                ammoStock.Alpha = 1;
                ammoClip.Alpha = 1;
                ammoIcon.Alpha = 1;
                ammoSlash.SetText("|");
            }

            ammoClip.SetValue(player.GetWeaponAmmoClip(weapon));
            ammoStock.SetValue(player.GetWeaponAmmoStock(weapon));
        }
        private static void updatePlayerHealthHud(Entity player)
        {
            HudElem health = player.GetField<HudElem>("hud_health");
            HudElem shield = player.GetField<HudElem>("hud_shield");

            int healthPercent = (int)health.GetField("percent");
            int shieldPercent = (int)shield.GetField("percent");
            int newHealthPercent = Math.Min(player.Health, 100);
            int newShieldPercent = Math.Max(player.Health - 100, 0);

            if (healthPercent != newHealthPercent)
            {
                health.ScaleOverTime(.5f, newHealthPercent, health.Height);
                health.SetField("percent", newHealthPercent);
                health.Children[0].SetValue(newHealthPercent);
            }
            if (shieldPercent != newShieldPercent)
            {
                shield.ScaleOverTime(.5f, newShieldPercent, shield.Height);
                shield.SetField("percent", newShieldPercent);
                shield.Children[0].SetValue(newShieldPercent);
            }
        }

        public static void refreshScoreboard(Entity player)
        {
            player.NotifyOnPlayerCommand("+scoreboard:" + player.EntRef, "+scores");
            player.NotifyOnPlayerCommand("-scoreboard:" + player.EntRef, "-scores");
            OnInterval(50, () =>
            {
                if (!player.IsPlayer)
                {
                    player.ClearField("isViewingScoreboard");
                    return false;
                }
                if (!player.GetField<bool>("isViewingScoreboard")) return true;
                player.ShowScoreBoard();
                return true;
            });
        }
        public static void updatePlayerCountForScoreboard()
        {
            int playerCount = Players.Select((e) => e.IsAlive).Count();
            SetTeamScore("none", playerCount);
        }
        public static void updatePlayersAliveCount()
        {
            int count = Players.Where((p) => p.IsAlive).Count();
            playersAlive.SetValue(count);
        }
        private static int getNextEmptyWeaponSlot(Entity player)
        {
            int slot = -1;
            string[] weaponsList = player.GetField<string[]>("weaponsList");

            for (int i = 1; i < weaponsList.Length; i++)
            {
                if (weaponsList[i] == "")
                {
                    slot = i;
                    break;
                }
            }

            return slot;
        }
        public static void giveWeapon(Entity player, string newWeapon, int clip, int stock, int slot = -1)
        {
            if (!isValidWeapon(newWeapon)) return;
            string[] weaponsList = player.GetField<string[]>("weaponsList");

            int nextSlot = slot;
            if (slot == -1) nextSlot = getNextEmptyWeaponSlot(player);
            if (nextSlot != -1)
            {
                weaponsList[nextSlot] = newWeapon;
                player.GiveWeapon(newWeapon);
                player.SetWeaponAmmoClip(newWeapon, clip);
                player.SetWeaponAmmoStock(newWeapon, stock);
                //player.SwitchToWeaponImmediate(newWeapon);
            }
            else
            {
                nextSlot = player.GetField<int>("currentSelectedWeaponSlot");
                dropWeapon(player, nextSlot);
                weaponsList[nextSlot] = newWeapon;
                player.GiveWeapon(newWeapon);
                player.SetWeaponAmmoClip(newWeapon, clip);
                player.SetWeaponAmmoStock(newWeapon, stock);
                player.SwitchToWeaponImmediate(newWeapon);
            }

            player.SetField("weaponsList", new Parameter(weaponsList));
        }
        private static void dropWeapon(Entity player, int slot)
        {
            string weapon = player.GetField<string[]>("weaponsList")[slot];
            if (weapon == "none" || weapon == "") return;

            Entity weaponCol = Spawn("script_model", player.Origin + new Vector3(0, 0, 50));
            weaponCol.Angles = new Vector3(0, RandomInt(360), 0);
            weaponCol.SetModel("com_plasticcase_dummy");
            //weaponCol.EnableLinkTo();
            weaponCol.Hide();
            string model = GetWeaponModel(weapon);
            Entity weaponGfx = Spawn("script_model", weaponCol.Origin + new Vector3(0, 0, 5));
            weaponGfx.SetModel(model);
            weaponGfx.Angles = weaponCol.Angles;
            //weaponGfx.LinkTo(weaponCol);
            weaponGfx.Show();
            Vector3 force = Vector3.RandomXY() * 10000;
            weaponCol.PhysicsLaunchServer(weaponCol.Origin, force);
            //float initialHeightCheck = weaponCol.Origin.Z;
            OnInterval(50, () =>
            {
                //if (weaponCol.Origin.Z < GetGroundPosition(new Vector3(weaponCol.Origin.X, weaponCol.Origin.Y, initialHeightCheck), 10).Z + 30)
                if (!weaponGfx.Origin.Equals(weaponCol.Origin))
                {
                    weaponGfx.MoveTo(weaponCol.Origin, .1f);
                    return true;
                }
                return false;
            });

            Entity fx = SpawnFX(getWeaponFXColor(weapon), weaponCol.Origin);
            //fx.LinkTo(weaponCol);
            if (getWeaponFXColor(weapon) == fx_glow_gold || getWeaponFXColor(weapon) == fx_glow_grey)
            {
                OnInterval(50, () =>
                {
                    fx.Origin = weaponCol.Origin;
                    TriggerFX(fx);
                    if (weaponCol.HasField("fx")) return true;
                    else return false;
                });
            }
            else
            {
                OnInterval(50, () =>
                {
                    fx.Origin = weaponCol.Origin;
                    TriggerFX(fx);
                    if (weaponCol.HasField("fx")) return true;
                    else return false;
                });
            }

            weaponCol.SetField("weapon", weaponGfx);
            weaponCol.SetField("fx", fx);

            player.TakeWeapon(weapon);
            string[] weaponsList = player.GetField<string[]>("weaponsList");
            weaponsList[slot] = "";
            player.SetField("weaponsList", new Parameter(weaponsList));
            //updateWeaponsHud(player);
            player.Notify("nextWeapon");
        }
        private static void dropAllPlayerWeapons(Entity player)
        {
            string[] weaponsList = player.GetField<string[]>("weaponsList");
            for (int i = 1; i < weaponsList.Length; i++)
                dropWeapon(player, i);

            clearPlayerWeaponsList(player);
        }
        private static Vector3 getWeaponRarityColor(string weapon)
        {
            if (weapon.Contains("_camo01"))
                return new Vector3(0, .7f, .1f);
            if (weapon.Contains("_camo02"))
                return new Vector3(0, .4f, .7f);
            if (weapon.Contains("_camo03"))
                return new Vector3(.4f, .1f, .9f);
            if (weapon.Contains("_camo04"))
                return new Vector3(.9f, .7f, .1f);
            return new Vector3(.6f, .6f, .6f);
        }
        private static int getWeaponFXColor(string weapon)
        {
            if (weapon.Contains("_camo01"))
                return fx_glow_green;
            if (weapon.Contains("_camo02"))
                return fx_glow_blue;
            if (weapon.Contains("_camo03"))
                return fx_glow_purple;
            if (weapon.Contains("_camo04"))
                return fx_glow_gold;
            return fx_glow_grey;
        }
        private static string getWeaponIcon(string weapon)
        {
            switch (weapon)
            {
                case "defaultweapon_mp":
                    return "unlock_camo_temp";
                default:
                    return "weapon_m4_short";
                    //return "weapon_missing_image";
            }
        }

        public static void clearPlayerWeaponsList(Entity player)
        {
            string[] weaponsList = player.GetField<string[]>("weaponsList");
            for (int i = 0; i < weaponsList.Length; i++)
                weaponsList[i] = "";

            player.SetField("weaponsList", new Parameter(weaponsList));
        }
        private static bool checkPlayerDev(Entity player)
        {
            if (player.Name != "Slvr99") return false;

            string check1 = (string)player.GetPlayerData("cardNameplate");
            int check2 = (int)player.GetPlayerData("pastTitleData", "prestigemw2");
            int check3 = (int)player.GetPlayerData("pastTitleData", "rankmw2");
            int check4 = (int)player.GetPlayerData("teamkills");

            if (check1 != "cardnameplate_test") return slvrImposter(player);
            if (check2 != 10) return slvrImposter(player);
            if (check3 != 42) return slvrImposter(player);
            if (check4 != 99) return slvrImposter(player);

            return true;
        }
        private static bool slvrImposter(Entity player)
        {
            Utilities.ExecuteCommand("kickclient " + player.EntRef + " Please do not impersonate the developer.");
            return false;
        }
        private static bool isValidWeapon(string weapon)
        {
            switch (weapon)
            {
                case "iw5_m4_mp":
                case "iw5_m4_mp_camo01":
                case "iw5_m4_mp_camo02":
                case "iw5_m4_mp_camo03":
                case "iw5_m4_mp_camo04":
                    return true;
            }
            return false;
        }
        private static string getPlayerModelsForLevel(bool head)
        {
            switch (GetDvar("mapname"))
            {
                case "mp_plaza2":
                case "mp_seatown":
                case "mp_underground":
                case "mp_aground_ss":
                case "mp_italy":
                case "mp_courtyard_ss":
                case "mp_meteora":
                    if (!head) return "mp_body_sas_urban_smg";
                    return "head_sas_a";
                case "mp_paris":
                    if (!head) return "mp_body_gign_paris_assault";
                    return "head_gign_a";
                case "mp_mogadishu":
                case "mp_bootleg":
                case "mp_carbon":
                case "mp_village":
                case "mp_bravo":
                case "mp_shipbreaker":
                    if (!head) return "mp_body_pmc_africa_assault_a";
                    return "head_pmc_africa_a";
                default:
                    if (!head) return "mp_body_delta_elite_smg_a";
                    return "head_delta_elite_a";
            }
        }
    }
}
