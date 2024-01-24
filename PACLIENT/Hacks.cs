using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using Steamworks.Data;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using static GameObjectManager;
using System.Windows.Forms;
using static UnityEngine.GraphicsBuffer;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;
using System.Runtime.InteropServices;
using UnityEngine.UIElements;
using Mono.CSharp;
using Enum = System.Enum;
using Event = UnityEngine.Event;
using Color = UnityEngine.Color;
using static ProjectApparatus.Features.Thirdperson;
using JetBrains.Annotations;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections;
using System.Text.RegularExpressions;
using Random = UnityEngine.Random;
using UnityEngine.UI;

namespace ProjectApparatus
{

    public static class LevenshteinDistance
    {
        /// <summary>
        ///     Calculate the difference between 2 strings using the Levenshtein distance algorithm
        /// </summary>
        /// <param name="source1">First string</param>
        /// <param name="source2">Second string</param>
        /// <returns></returns>
        public static int Calculate(string source1, string source2) //O(n*m)
        {
            var source1Length = source1.Length;
            var source2Length = source2.Length;

            var matrix = new int[source1Length + 1, source2Length + 1];

            // First calculation, if one entry is empty return full length
            if (source1Length == 0)
                return source2Length;

            if (source2Length == 0)
                return source1Length;

            // Initialization of matrix with row size source1Length and columns size source2Length
            for (var i = 0; i <= source1Length; matrix[i, 0] = i++) { }
            for (var j = 0; j <= source2Length; matrix[0, j] = j++) { }

            // Calculate rows and collumns distances
            for (var i = 1; i <= source1Length; i++)
            {
                for (var j = 1; j <= source2Length; j++)
                {
                    var cost = (source2[j - 1] == source1[i - 1]) ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }
            // return result
            return matrix[source1Length, source2Length];
        }
    }

    public class MouseSimulator
    {
        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);
    }

    internal class Hacks : MonoBehaviour
    {
        private bool topRightCamera = false;
        private float cameraOffsetY = 3f;
        GameObject cameraObject = null;
        public bool monitorCamera = false;
        GameObject monitorCameraObject = null;
        GameObject teleporter = GameObject.Find("Teleporter(Clone)");
        GameObject inverse = GameObject.Find("InverseTeleporter(Clone)");

        bool upPressed = false;
        bool downPressed = false;

        private static GUIStyle Style = null;
        private readonly SettingsData settingsData = Settings.Instance.settingsData;

        bool IsPlayerValid(PlayerControllerB plyer)
        {
            return (plyer != null &&
                    !plyer.disconnectedMidGame &&
                    !plyer.playerUsername.Contains("Player #"));
        }

        public static Hacks Inst2
        {
            get
            {
                if (Hacks.instance == null)
                {
                    Hacks.instance = new Hacks();
                }
                return Hacks.instance;
            }
        }

        public void OnGUI()
        {
            if (!Settings.Instance.b_isMenuOpen && Event.current.type != EventType.Repaint)
                return;

            UI.Reset();

            Color darkBackground = new Color(23f / 255f, 23f / 255f, 23f / 255f, 1f);

            GUI.backgroundColor = darkBackground;
            GUI.contentColor = Color.white;

            Style = new GUIStyle(GUI.skin.label);
            Style.normal.textColor = Color.white;
            Style.fontStyle = FontStyle.Bold;

            if (settingsData.b_EnableESP)
            {
                DisplayLoot();
                DisplayPlayers();
                DisplayDoors();
                DisplayLandmines();
                DisplayTurrets();
                DisplaySteamHazard();
                DisplayEnemyAI();
                DisplayShip();
                DisplayDeadPlayers();
            }

            Vector2 centeredPos = new Vector2(UnityEngine.Screen.width / 2f, UnityEngine.Screen.height / 2f);

            GUI.color = settingsData.c_Theme;

            if (settingsData.b_CenteredIndicators)
            {
                float iY = Settings.TEXT_HEIGHT;
                if (settingsData.b_DisplayGroupCredits && Instance.shipTerminal != null) Render.String(Style, centeredPos.x, centeredPos.y + 7 + iY, 150f, Settings.TEXT_HEIGHT, "Group Credits: " + Instance.shipTerminal.groupCredits, GUI.color, true, true); iY += Settings.TEXT_HEIGHT - 10f;
                if (settingsData.b_DisplayQuota && TimeOfDay.Instance) Render.String(Style, centeredPos.x, centeredPos.y + 7 + iY, 150f, Settings.TEXT_HEIGHT, "Profit Quota: " + TimeOfDay.Instance.quotaFulfilled + "/" + TimeOfDay.Instance.profitQuota, GUI.color, true, true); iY += Settings.TEXT_HEIGHT - 10f;
                if (settingsData.b_DisplayDaysLeft && TimeOfDay.Instance) Render.String(Style, centeredPos.x, centeredPos.y + 7 + iY, 150f, Settings.TEXT_HEIGHT, "Days Left: " + TimeOfDay.Instance.daysUntilDeadline, GUI.color, true, true); iY += Settings.TEXT_HEIGHT - 10f;
            }

            string Watermark = "Project Apparatus";
            Watermark += " | v" + settingsData.version;
            if (!Settings.Instance.b_isMenuOpen) Watermark += " | Press INSERT";
            if (!settingsData.b_CenteredIndicators)
            {
                if (settingsData.b_DisplayGroupCredits && Instance.shipTerminal != null)
                    Watermark += $" | Group Credits: {Instance.shipTerminal.groupCredits}";
                if (settingsData.b_DisplayQuota && TimeOfDay.Instance)
                    Watermark += $" | Profit Quota: {TimeOfDay.Instance.quotaFulfilled} / {TimeOfDay.Instance.profitQuota}";
                if (settingsData.b_DisplayDaysLeft && TimeOfDay.Instance)
                    Watermark += $" | Days Left: {TimeOfDay.Instance.daysUntilDeadline}"; ;
            }

            Render.String(Style, 10f, 5f, 150f, Settings.TEXT_HEIGHT, Watermark, GUI.color);

            if (Settings.Instance.b_isMenuOpen)
            {
                Settings.Instance.windowRect = GUILayout.Window(0, Settings.Instance.windowRect, new GUI.WindowFunction(MenuContent), "Project Apparatus", Array.Empty<GUILayoutOption>());
            }

            if (settingsData.b_Crosshair)
            {
                Render.FilledCircle(centeredPos, 5, Color.black);
                Render.FilledCircle(centeredPos, 3, settingsData.c_Theme);
            }
        }

        private PlayerControllerB selectedPlayer = null;

        public static float Round(float value, int digits)
        {
            float mult = Mathf.Pow(10.0f, (float)digits);
            return Mathf.Round(value * mult) / mult;
        }

        private void MenuContent(int windowID)
        {

            GUILayout.BeginHorizontal();
            UI.Tab("Start", ref UI.nTab, UI.Tabs.Start);
            UI.Tab("Self", ref UI.nTab, UI.Tabs.Self);
            UI.Tab("Misc", ref UI.nTab, UI.Tabs.Misc);
            UI.Tab("ESP", ref UI.nTab, UI.Tabs.ESP);
            UI.Tab("Players", ref UI.nTab, UI.Tabs.Players);
            UI.Tab("Graphics", ref UI.nTab, UI.Tabs.Graphics);
            UI.Tab("Upgrades", ref UI.nTab, UI.Tabs.Upgrades);
            UI.Tab("Settings", ref UI.nTab, UI.Tabs.Settings);
            GUILayout.EndHorizontal();


            UI.TabContents("Start", UI.Tabs.Start, () =>
            {
                GUILayout.Label($"Welcome to Project Apparatus v{settingsData.version}!\n\n" +
                                $"If you have suggestions, please create a pull request in the repo or reply to the UC thread.\n" +
                                $"If you find bugs, please provide some steps on how to reproduce the problem and create an issue or pull request in the repo or reply to the UC thread");
                GUILayout.Space(20f);
                GUILayout.Label($"Changelog {settingsData.version}", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(300f));
                GUILayout.TextArea(Settings.Changelog.changes.ToString(), GUILayout.ExpandHeight(true));
                GUILayout.EndScrollView();
                GUILayout.Space(20f);
                GUILayout.Label($"Credits", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                GUILayout.Label(Settings.Credits.credits.ToString());
            });



            UI.TabContents("Self", UI.Tabs.Self, () =>
            {
                UI.Checkbox(ref settingsData.b_GodMode, "God Mode", "Prevents you from taking any damage.");
                UI.Checkbox(ref settingsData.b_NoFlash, "No flash", "Prevents you from being flashbanged");
                UI.Checkbox(ref settingsData.b_InfiniteStam, "Infinite Stamina", "Prevents you from losing any stamina.");
                UI.Checkbox(ref settingsData.b_InfiniteCharge, "Infinite Charge", "Prevents your items from losing any charge.");
                UI.Checkbox(ref settingsData.b_InfiniteZapGun, "Infinite Zap Gun", "Infinitely stuns enemies with the zap-gun.");
                UI.Checkbox(ref settingsData.b_InfiniteShotgunAmmo, "Infinite Shotgun Ammo", "Prevents you from out of ammo.");
                UI.Checkbox(ref settingsData.b_InfiniteItems, "Infinite Item Use", "Allows you to infinitely use items like the gift box and stun grenade. (Buggy)");
                UI.Checkbox(ref settingsData.b_RemoveWeight, "No Weight", "Removes speed limitations caused by item weight.");
                UI.Checkbox(ref settingsData.b_InteractThroughWalls, "Interact Through Walls", "Allows you to interact with anything through walls.");
                UI.Checkbox(ref settingsData.b_UnlimitedGrabDistance, "No Grab Distance Limit", "Allows you to interact with anything no matter the distance.");
                UI.Checkbox(ref settingsData.b_OneHandAllObjects, "One Hand All Objects", "Allows you to one-hand any two-handed objects.");
                UI.Checkbox(ref settingsData.b_OneHandAnimations, "One Hand animations", "Should the animations for each object be 1 hand?");
                UI.Checkbox(ref settingsData.b_DisableFallDamage, "Disable Fall Damage", "You no longer take fall damage.");
                UI.Checkbox(ref settingsData.b_DisableInteractCooldowns, "Disable Interact Cooldowns", "Disables all interact cooldowns (e.g., noisemakers, toilets, etc).");
                UI.Checkbox(ref settingsData.b_InstantInteractions, "Instant Interactions", "Makes all hold interactions instantaneous.");
                UI.Checkbox(ref settingsData.b_PlaceAnywhere, "Place Anywhere", "Place objects from the ship anywhere you want.");
                UI.Checkbox(ref settingsData.b_TauntSlide, "Taunt Slide", "Allows you to emote and move at the same time.");
                UI.Checkbox(ref settingsData.b_FastLadderClimbing, "Fast Ladder Climbing", "Instantly climbs up ladders.");
                UI.Checkbox(ref settingsData.b_HearEveryone, "Hear Everyone", "Allows you to hear everyone no matter the distance.");
                UI.Checkbox(ref settingsData.b_ChargeAnyItem, "Charge Any Item", "Allows you to put any grabbable item in the charger.");
                UI.Checkbox(ref settingsData.b_NightVision, $"Night Vision ({settingsData.i_NightVision}%)", "Allows you to see in the dark.");
                settingsData.i_NightVision = Mathf.RoundToInt(GUILayout.HorizontalSlider(settingsData.i_NightVision, 1, 100));

                UI.Checkbox(ref settingsData.b_bhop, $"Bhop ({settingsData.i_bhopSprint})", "Yeah i know...");
                settingsData.i_bhopSprint = GUILayout.HorizontalSlider(settingsData.i_bhopSprint, 0, 20);

                UI.Checkbox(ref settingsData.b_WalkSpeed, $"Adjust Walk Speed ({settingsData.i_WalkSpeed})", "Allows you to modify your walk speed.");
                settingsData.i_WalkSpeed = GUILayout.HorizontalSlider(settingsData.i_WalkSpeed, 0, 20);
                UI.Checkbox(ref settingsData.b_SprintSpeed, $"Adjust Sprint Speed ({settingsData.i_SprintSpeed})", "Allows you to modify your sprint speed.");
                settingsData.i_SprintSpeed = GUILayout.HorizontalSlider(settingsData.i_SprintSpeed, 1, 20);
                UI.Checkbox(ref settingsData.b_JumpHeight, $"Adjust Jump Height ({settingsData.i_JumpHeight})", "Allows you to modify your jump height.");
                settingsData.i_JumpHeight = GUILayout.HorizontalSlider(settingsData.i_JumpHeight, 1, 100);

                UI.Button("Doom mode", "Trust me, its worth it.", () =>
                {
                    DoomMode();
                });

                UI.Button("Suicide", "Kills local player.", () =>
                {
                    Instance.localPlayer.DamagePlayerFromOtherClientServerRpc(100, new Vector3(), -1);
                });

                UI.Button("Respawn", "Respawns you. You will be invisible to both players and enemies.", () =>
                {
                    Features.Misc.RespawnLocalPlayer();
                });

                UI.Button("Teleport To Ship", "Teleports you into the ship.", () =>
                {
                    if (Instance.shipRoom)
                        Instance.localPlayer?.TeleportPlayer(Instance.shipRoom.transform.position);
                });

                UI.Button("Possess Nearest Enemy", "Possesses the nearest enemy. (Note: You will be visibily within the enemy.)", () =>
                {
                    Features.Possession.StartPossession();
                });

                UI.Button("Stop Possessing", "Stops possessing the currently possessed enemy.", () =>
                {
                    Features.Possession.StopPossession();
                });

                GUILayout.BeginHorizontal();
                UI.Checkbox(ref settingsData.b_Noclip, $"Noclip ({settingsData.fl_NoclipSpeed})", "Allows you to fly and clip through walls.");
                UI.Keybind(ref settingsData.keyNoclip);
                GUILayout.EndHorizontal();
                settingsData.fl_NoclipSpeed = Mathf.RoundToInt(GUILayout.HorizontalSlider(settingsData.fl_NoclipSpeed, 1, 100));
            });

            UI.TabContents("Misc", UI.Tabs.Misc, () =>
            {
                UI.Checkbox(ref settingsData.b_NoMoreCredits, "No More Credits", "Prevents your group from receiving any credits. (Doesn't apply to quota)");
                UI.Checkbox(ref settingsData.b_SensitiveLandmines, "Sensitive Landmines", "Automatically detonates landmines when a player is in kill range.");
                UI.Checkbox(ref settingsData.b_AllJetpacksExplode, "All Jetpacks Explode", "When a player tries to equip a jetpack they will be greeted with an explosion.");
                UI.Checkbox(ref settingsData.b_LightShow, "Light Show", "Rapidly turns on/off the light switch and TV.");
                UI.Checkbox(ref settingsData.b_TerminalNoisemaker, "Terminal Noisemaker", "Plays a very annoying noise from the terminal.");
                UI.Checkbox(ref settingsData.b_AlwaysShowClock, "Always Show Clock", "Displays the clock even when you are in the facility.");
                UI.Checkbox(ref settingsData.b_FreeShit, "Free Store", "Store is now free!");
                UI.Checkbox(ref settingsData.b_noButtonCooldown, "No button cooldown", "Teleporters no longer have cooldowns");
                UI.Checkbox(ref settingsData.b_inspectAnything, "Inspect anything", "You can inspect any object");
                UI.Checkbox(ref settingsData.b_playSound, "Custom sound", "Play custom sound when pocketting item");

                GUILayout.Label($"Player to teleport: {StartOfRound.Instance.mapScreen.targetedPlayer.playerUsername}", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

                UI.Button("Teleport to ship", "Teleports current player on monitor using the teleporter.", () =>
                {
                    PlayerControllerB playerToBeamUp = StartOfRound.Instance.mapScreen.targetedPlayer;
                    BtnTeleport();
                });

                UI.Button("Inverse Teleport", "Use inverse teleport", () =>
                {
                    BtnInverseTeleport();
                });

                settingsData.str_ChatMessage = GUILayout.TextField(settingsData.str_ChatMessage, Array.Empty<GUILayoutOption>());
                UI.Button("Send Message", "Anonymously sends a message in chat.", () =>
                {
                    PAUtils.SendChatMessage(settingsData.str_ChatMessage);
                });

                UI.Checkbox(ref settingsData.b_AnonChatSpam, "Spam Message", "Anonymously spams a message in chat.");

                settingsData.str_TerminalSignal = GUILayout.TextField(settingsData.str_TerminalSignal, Array.Empty<GUILayoutOption>());
                UI.Button("Send Signal", "Remotely sends a signal.", () =>
                {
                    if (!StartOfRound.Instance.unlockablesList.unlockables[(int)UnlockableUpgrade.SignalTranslator].hasBeenUnlockedByPlayer)
                    {
                        StartOfRound.Instance.BuyShipUnlockableServerRpc((int)UnlockableUpgrade.SignalTranslator, Instance.shipTerminal.groupCredits);
                        StartOfRound.Instance.SyncShipUnlockablesServerRpc();
                    }
               
                    HUDManager.Instance.UseSignalTranslatorServerRpc(settingsData.str_TerminalSignal);
                });

                settingsData.str_SpawnMessage = GUILayout.TextField(settingsData.str_SpawnMessage, Array.Empty<GUILayoutOption>());
                UI.Button("Spawn Item", "Spawn an Item by name", () =>
                {
                    string itemName = SpawnItem(settingsData.str_SpawnMessage);
                    if (itemName == "Not host")
                    {
                        PAUtils.SendChatMessage($"Can't spawn as client.");
                        return;
                    }

                    if (itemName == "true")
                    {
                        PAUtils.SendChatMessage($"Item <{settingsData.str_SpawnMessage}> spawned.");
                    } else
                    {
                        PAUtils.SendChatMessage($"Trouble spawning <{settingsData.str_SpawnMessage}>.. Did you mean {itemName}?");
                    }
                });

                GUILayout.Label("ARGS: name, coords (here for your position), count, outside?");
                GUILayout.Label("Example: Flowerman, 56.24.10, 1, false");
                settingsData.str_ESpawnMessage = GUILayout.TextField(settingsData.str_ESpawnMessage, Array.Empty<GUILayoutOption>());
                UI.Button("Spawn Enemy", "Spawn an enemy by name", () =>
                {
                    string input = settingsData.str_ESpawnMessage;
                    string[] splitInput = input.Split(new string[] { ", " }, StringSplitOptions.None);
                    Vector3 position;
                    if (splitInput[1] == "here")
                    {
                        position = Instance.localPlayer.transform.position;
                    }
                    else
                    {
                        string[] coordinates = splitInput[1].Split('.');
                        if (coordinates.Length == 3 && float.TryParse(coordinates[0], out float x) && float.TryParse(coordinates[1], out float y) && float.TryParse(coordinates[2], out float z))
                        {
                            position = new Vector3(x, y, z);
                        }
                        else
                        {
                            throw new ArgumentException("Invalid Vector3 format");
                        }
                    }
                    int number;
                    if (Int32.TryParse(splitInput[2], out number))
                    {
                        splitInput[2] = number.ToString();
                    }
                    bool boolean;
                    if (Boolean.TryParse(splitInput[3], out boolean))
                    {
                        splitInput[3] = boolean.ToString();
                    }

                    string itemName = SpawnEnemyManager(splitInput[0], position, number, boolean);
                    if (itemName == "Not host")
                    {
                        PAUtils.SendChatMessage($"Can't spawn as client.");
                        return;
                    }

                    if (itemName == "true")
                    {
                        PAUtils.SendChatMessage($"<{splitInput[0]}> spawned.");
                    }
                    else
                    {
                        PAUtils.SendChatMessage($"Trouble spawning <{settingsData.str_ESpawnMessage}>.. Did you mean {itemName}");
                    }
                });

                settingsData.str_ItemVal = GUILayout.TextField(settingsData.str_ItemVal, Array.Empty<GUILayoutOption>());
                UI.Button("Set value", "Set item value (local instance only)", () =>
                {
                    SetItemValue(int.Parse(settingsData.str_ItemVal));
                });

                if (!settingsData.b_NoMoreCredits)
                {
                    settingsData.str_MoneyToGive = GUILayout.TextField(settingsData.str_MoneyToGive, Array.Empty<GUILayoutOption>());
                    UI.Button("Give Credits", "Give your group however many credits you want. (Doesn't apply to quota)", () =>
                    {
                        if (Instance.shipTerminal)
                        {
                            Instance.shipTerminal.groupCredits += int.Parse(settingsData.str_MoneyToGive);
                            Instance.shipTerminal.SyncGroupCreditsServerRpc(Instance.shipTerminal.groupCredits, 
                                Instance.shipTerminal.numberOfItemsInDropship);
                        }
                    });

                    GUILayout.BeginHorizontal();
                    settingsData.str_QuotaFulfilled = GUILayout.TextField(settingsData.str_QuotaFulfilled, GUILayout.Width(42));
                    GUILayout.Label("/", GUILayout.Width(4));
                    settingsData.str_Quota = GUILayout.TextField(settingsData.str_Quota, GUILayout.Width(42));
                    GUILayout.EndHorizontal();

                    UI.Button("Set Quota", "Allows you to set the quota. (Host only)", () =>
                    {
                        if (TimeOfDay.Instance)
                        {
                            TimeOfDay.Instance.profitQuota = int.Parse(settingsData.str_Quota);
                            TimeOfDay.Instance.quotaFulfilled = int.Parse(settingsData.str_QuotaFulfilled);
                            TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
                        }
                    });
                }

                UI.Button($"Teleport All Items ({Instance.items.Count})", "Teleports all items on the planet to you.", () =>
                {
                    TeleportAllItems();
                });

                UI.Button("Land Ship", "Lands the ship.", () => StartOfRound.Instance.StartGameServerRpc());
                UI.Button("Start Ship", "Ship will leave the planet it's currently on.", () => StartOfRound.Instance.EndGameServerRpc(0));
                UI.Button("Unlock All Doors", "Unlocks all locked doors.", () =>
                {
                    foreach (DoorLock obj in Instance.doorLocks) 
                        obj?.UnlockDoorSyncWithServer();
                });
                UI.Button("Open All Mechanical Doors", "Opens all mechanical doors.", () =>
                {
                    foreach (TerminalAccessibleObject obj in Instance.bigDoors)
                        obj?.SetDoorOpenServerRpc(true);
                });
                UI.Button("Close All Mechanical Doors", "Closes all mechanical doors.", () =>
                {
                    foreach (TerminalAccessibleObject obj in Instance.bigDoors)
                        obj?.SetDoorOpenServerRpc(false);
                });
                UI.Button("Explode All Mines", "Explodes every single mine on the level.", () =>
                {
                    foreach (Landmine obj in Instance.landmines)
                        obj?.ExplodeMineServerRpc();
                });
                UI.Button("Kill All Enemies", "Kills all enemies.", () =>
                {
                    foreach (EnemyAI obj in Instance.enemies)
                        obj?.KillEnemyServerRpc(false);
                });
                UI.Button("Delete All Enemies", "Deletes all enemies.", () =>
                {
                    foreach (EnemyAI obj in Instance.enemies)
                        obj?.KillEnemyServerRpc(true);
                });
                UI.Button("Attack Players at Deposit Desk", "Forces the tentacle monster to attack, killing a nearby player.", () =>
                {
                    if (Instance.itemsDesk)
                        Instance.itemsDesk.AttackPlayersServerRpc();
                });
            });


            UI.TabContents("ESP", UI.Tabs.ESP, () =>
            {
                UI.Checkbox(ref settingsData.b_EnableESP, "Enabled", "Enables the ESP.");
                UI.Checkbox(ref settingsData.b_ItemESP, "Items", "Shows all items.");
                UI.Checkbox(ref settingsData.b_EnemyESP, "Enemies", "Shows all enemies.");
                UI.Checkbox(ref settingsData.b_PlayerESP, "Players", "Shows all players.");
                UI.Checkbox(ref settingsData.b_ShipESP, "Ships", "Shows the ship.");
                UI.Checkbox(ref settingsData.b_DoorESP, "Doors", "Shows all doors.");
                UI.Checkbox(ref settingsData.b_SteamHazard, "Steam Hazards", "Shows all hazard zones.");
                UI.Checkbox(ref settingsData.b_LandmineESP, "Landmines", "Shows all landmines.");
                UI.Checkbox(ref settingsData.b_TurretESP, "Turrets", "Shows all turrets.");
                UI.Checkbox(ref settingsData.b_DisplayHP, "Show Health", "Shows players' health.");
                UI.Checkbox(ref settingsData.b_DisplayWorth, "Show Value", "Shows items' value.");
                UI.Checkbox(ref settingsData.b_DisplayDistance, "Show Distance", "Shows the distance between you and the entity.");
                UI.Checkbox(ref settingsData.b_DisplaySpeaking, "Show Is Speaking", "Shows if the player is speaking.");
                UI.Checkbox(ref settingsData.b_AimbotEnabled, "Aimbot", "Proudly finished! Snaps to nearest enemy on screen");
                UI.Dropdown(["Enemy", "Player"], ref settingsData.str_AimbotMethod);
                UI.Checkbox(ref settingsData.b_WallCheck, "Wall check", "Should snap through walls?");

                UI.Checkbox(ref settingsData.b_ItemDistanceLimit, "Item Distance Limit (" + Mathf.RoundToInt(settingsData.fl_ItemDistanceLimit) + ")", "Toggle to set the item distance limit.");
                settingsData.fl_ItemDistanceLimit = GUILayout.HorizontalSlider(settingsData.fl_ItemDistanceLimit, 50, 500, Array.Empty<GUILayoutOption>());

                UI.Checkbox(ref settingsData.b_EnemyDistanceLimit, "Enemy Distance Limit (" + Mathf.RoundToInt(settingsData.fl_EnemyDistanceLimit) + ")", "Toggle to set the enemy distance limit.");
                settingsData.fl_EnemyDistanceLimit = GUILayout.HorizontalSlider(settingsData.fl_EnemyDistanceLimit, 50, 500, Array.Empty<GUILayoutOption>());

                UI.Checkbox(ref settingsData.b_MineDistanceLimit, "Landmine Distance Limit (" + Mathf.RoundToInt(settingsData.fl_MineDistanceLimit) + ")", "Toggle to set the landmine distance limit.");
                settingsData.fl_MineDistanceLimit = GUILayout.HorizontalSlider(settingsData.fl_MineDistanceLimit, 50, 500, Array.Empty<GUILayoutOption>());

                UI.Checkbox(ref settingsData.b_TurretDistanceLimit, "Turret Distance Limit (" + Mathf.RoundToInt(settingsData.fl_TurretDistanceLimit) + ")", "Toggle to set the turret distance limit.");
                settingsData.fl_TurretDistanceLimit = GUILayout.HorizontalSlider(settingsData.fl_TurretDistanceLimit, 50, 500, Array.Empty<GUILayoutOption>());
            });

            UI.TabContents(null, UI.Tabs.Players, () =>
            {
                GUILayout.BeginHorizontal();
                foreach (PlayerControllerB player in Instance.players)
                {
                    if (!IsPlayerValid(player)) continue;
                    UI.Tab(PAUtils.TruncateString(player.playerUsername, 12), ref selectedPlayer, player, true);
                }
                GUILayout.EndHorizontal();

                if (!IsPlayerValid(selectedPlayer))
                    selectedPlayer = null;

                if (selectedPlayer)
                {
                    UI.Header("Selected Player: " + selectedPlayer.playerUsername);
                    Settings.Instance.InitializeDictionaries(selectedPlayer);

                    // We keep toggles outside of the isPlayerDead check so that users can toggle them on/off no matter their condition.

                    bool DemigodCheck = Settings.Instance.b_DemiGod[selectedPlayer];
                    UI.Checkbox(ref DemigodCheck, "Demigod", "Automatically refills the selected player's health if below zero.");
                    Settings.Instance.b_DemiGod[selectedPlayer] = DemigodCheck;

                    bool ObjectSpam = Settings.Instance.b_SpamObjects[selectedPlayer];
                    UI.Checkbox(ref ObjectSpam, "Object Spam", "Spam places objects on the player to annoy/trap them.");
                    Settings.Instance.b_SpamObjects[selectedPlayer] = ObjectSpam;

                    UI.Checkbox(ref Settings.Instance.b_HideObjects, "Hide Objects", "Hides spammed objects from the selected player.");

                    if (!selectedPlayer.isPlayerDead)
                    {
                        UI.Button("Kill", "Kills the currently selected player.", () => { selectedPlayer.DamagePlayerFromOtherClientServerRpc(selectedPlayer.health + 1, new Vector3(900, 900, 900), 0); });
                        UI.Button("Teleport To", "Teleports you to the currently selected player.", () => { Instance.localPlayer.TeleportPlayer(selectedPlayer.playerGlobalHead.position); });
                        UI.Button("Teleport Enemies To", "Teleports all enemies to the currently selected player.", () =>
                        {
                            foreach (EnemyAI enemy in Instance.enemies)
                            {
                                if (enemy != null && enemy != Features.Possession.possessedEnemy)
                                {
                                    enemy.ChangeEnemyOwnerServerRpc(Instance.localPlayer.actualClientId);
                                    foreach (Collider col in enemy.GetComponentsInChildren<Collider>()) col.enabled = false; // To prevent enemies from getting stuck in eachother
                                    enemy.transform.position = selectedPlayer.transform.position;
                                    enemy.SyncPositionToClients();
                                }
                            }
                        });
                        UI.Button("Teleport Player To Ship", "Teleports the selected into the ship. (Host only)", () =>
                        {
                            Instance.shipTeleporter.TeleportPlayerOutServerRpc((int)selectedPlayer.playerClientId, Instance.shipRoom.transform.position);
                        });

                        UI.Button("Aggro Enemies", "Makes enemies target the selected player.\nDoesn't work on most monsters, works best on Crawlers & Spiders.", () => { 
                            foreach (EnemyAI enemy in Instance.enemies)
                            {
                                enemy.SwitchToBehaviourServerRpc(1); // I believe this just angers all enemies.
                                if (enemy.GetType() == typeof(CrawlerAI))
                                {
                                    CrawlerAI crawler = (CrawlerAI)enemy;
                                    crawler.BeginChasingPlayerServerRpc((int)selectedPlayer.playerClientId);
                                }
                                if (enemy.GetType() == typeof(NutcrackerEnemyAI))
                                {
                                    NutcrackerEnemyAI nutcracker = (NutcrackerEnemyAI)enemy;
                                    nutcracker.SwitchTargetServerRpc((int)selectedPlayer.playerClientId);
                                }
                                if (enemy.GetType() == typeof(CentipedeAI))
                                {
                                    CentipedeAI centipede = (CentipedeAI)enemy;
                                    centipede.TriggerCentipedeFallServerRpc(selectedPlayer.actualClientId);
                                }
                                if (enemy.GetType() == typeof(SandSpiderAI))
                                {
                                    SandSpiderAI spider = (SandSpiderAI)enemy;
                                    foreach (SandSpiderWebTrap trap in spider?.webTraps)
                                        if (trap)
                                            spider?.PlayerTripWebServerRpc(trap.trapID, (int)selectedPlayer.playerClientId);
                                }
                            }
                        });

                        Settings.Instance.str_DamageToGive = GUILayout.TextField(Settings.Instance.str_DamageToGive, Array.Empty<GUILayoutOption>());
                        UI.Button("Damage", "Damages the player for a given amount.", () => { selectedPlayer.DamagePlayerFromOtherClientServerRpc(int.Parse(Settings.Instance.str_DamageToGive), new Vector3(900, 900, 900), 0); });

                        Settings.Instance.str_HealthToHeal = GUILayout.TextField(Settings.Instance.str_HealthToHeal, Array.Empty<GUILayoutOption>());
                        UI.Button("Heal", "Heals the player for a given amount.", () => { selectedPlayer.DamagePlayerFromOtherClientServerRpc(-int.Parse(Settings.Instance.str_HealthToHeal), new Vector3(900, 900, 900), 0); });
                    }

                    Settings.Instance.str_ChatAsPlayer = GUILayout.TextField(Settings.Instance.str_ChatAsPlayer, Array.Empty<GUILayoutOption>());
                    UI.Button("Send Message", "Sends a message in chat as the selected player.", () =>
                    {
                        PAUtils.SendChatMessage(Settings.Instance.str_ChatAsPlayer, (int)selectedPlayer.playerClientId);
                    });

                    bool SpamChatCheck = Settings.Instance.b_SpamChat[selectedPlayer];
                    UI.Checkbox(ref SpamChatCheck, "Spam Message", "Spams the message in chat as the selected player.");
                    Settings.Instance.b_SpamChat[selectedPlayer] = SpamChatCheck;

                    UI.Button("Steam Profile", "Opens the selected player's steam profile in your overlay.", () => { SteamFriends.OpenUserOverlay(selectedPlayer.playerSteamId, "steamid");});
                    UI.Button("Get Name", "Attempts to get user's real name", () => {string name = SUtils.FetchUserName(selectedPlayer.playerSteamId); PAUtils.SendChatMessage(name); });
                }
            });

            if (StartOfRound.Instance && Instance.shipTerminal)
            {
                UI.TabContents("Upgrades", UI.Tabs.Upgrades, () =>
                {
                    bool allUpgradesUnlocked = true;
                    bool allSuitsUnlocked = true;

                    for (int i = 0; i < StartOfRound.Instance.unlockablesList.unlockables.Count; i++)
                    {
                        if (Enum.IsDefined(typeof(UnlockableUpgrade), i) &&
                            !StartOfRound.Instance.unlockablesList.unlockables[i].hasBeenUnlockedByPlayer)
                        {
                            allUpgradesUnlocked = false;
                            break;
                        }
                    }

                    for (int i = 1; i <= 3; i++)
                    {
                        if (!StartOfRound.Instance.unlockablesList.unlockables[i]?.hasBeenUnlockedByPlayer ?? false)
                        {
                            allSuitsUnlocked = false;
                            break;
                        }
                    }

                    if (allUpgradesUnlocked && allSuitsUnlocked)
                    {
                        GUILayout.Label("You've already unlocked all upgrades.");
                    }
                    else
                    {
                        UI.Button("Unlock All Upgrades", "Unlocks all ship upgrades.", () =>
                        {
                            for (int i = 0; i < StartOfRound.Instance.unlockablesList.unlockables.Count; i++)
                            {
                                if (Enum.IsDefined(typeof(UnlockableUpgrade), i) &&
                                    !StartOfRound.Instance.unlockablesList.unlockables[i].hasBeenUnlockedByPlayer)
                                {
                                    StartOfRound.Instance.BuyShipUnlockableServerRpc(i, Instance.shipTerminal.groupCredits);
                                    StartOfRound.Instance.SyncShipUnlockablesServerRpc();
                                }
                            }
                        });

                        if (!allSuitsUnlocked)
                        {
                            UI.Button("Unlock All Suits", "Unlocks all suits.", () =>
                            {
                                for (int i = 1; i <= 3; i++)
                                {
                                    StartOfRound.Instance.BuyShipUnlockableServerRpc(i, Instance.shipTerminal.groupCredits);
                                }
                            });
                        }

                        for (int i = 0; i < StartOfRound.Instance.unlockablesList.unlockables.Count; i++)
                        {
                            if (Enum.IsDefined(typeof(UnlockableUpgrade), i) &&
                                !StartOfRound.Instance.unlockablesList.unlockables[i].hasBeenUnlockedByPlayer)
                            {
                                string unlockableName = PAUtils.ConvertFirstLetterToUpperCase(StartOfRound.Instance.unlockablesList.unlockables[i].unlockableName);

                                UI.Button(unlockableName, $"Unlock {unlockableName}", () =>
                                {
                                    StartOfRound.Instance.BuyShipUnlockableServerRpc(i, Instance.shipTerminal.groupCredits);
                                    StartOfRound.Instance.SyncShipUnlockablesServerRpc();
                                });
                            }
                        }
                    }
                });
            }

            UI.TabContents("Graphics", UI.Tabs.Graphics, () =>
            {
                UI.Checkbox(ref settingsData.b_DisableFog, "Disable Fog", "Disables the fog effect.");
                UI.Checkbox(ref settingsData.b_DisableDepthOfField, "Disable Depth of Field", "Disables the depth of field effect.");
                if (UI.Checkbox(ref settingsData.b_RemoveVisor, "Disable Visor", "Disables the visor from your helmet in first person."))
                {
                    if (!settingsData.b_RemoveVisor && !Features.Thirdperson.ThirdpersonCamera.ViewState)
                        Instance.localVisor?.SetActive(true);
                }
                UI.Checkbox(ref settingsData.b_CameraResolution, "Full Render Resolution", "Forces the game to render in full resolution.\n<color=#ff0000>You will need to leave the game for this to activate.</color>");
                GUILayout.Label($"Field of View ({settingsData.i_FieldofView})");
                settingsData.i_FieldofView = Mathf.RoundToInt(GUILayout.HorizontalSlider(settingsData.i_FieldofView, 50, 110, Array.Empty<GUILayoutOption>()));

                GUILayout.BeginHorizontal();
                UI.Button("Top Cam", "Spawns a camera above you, controll its height with up/down arrow.", () =>
                {
                    CreateTopRightCamera();
                });
                
                UI.Button("Delete Cam", "Deletes top cam", () =>
                {
                    DeleteTopCam();
                });

                UI.Button("Reset Cam", "Resets cam rotation to player rotation", () =>
                {
                    ResetCam();
                });
                GUILayout.EndHorizontal();

                GUILayout.Label($"Player to teleport: {StartOfRound.Instance.mapScreen.targetedPlayer.playerUsername}", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

                GUILayout.BeginHorizontal();
                UI.Button("Monitor Cam", "Spawns a camera at the monitor wall.", () =>
                {
                    CreateMonitorCamera();
                });

                UI.Button("Delete Cam", "Deletes monitor cam", () =>
                {
                    DeleteMonitorCam();
                });

                UI.Button("Switch Cam", "Switch Monitor Camera", () =>
                {
                    SwitchCam();
                });
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Thirdperson");
                UI.Keybind(ref settingsData.keyThirdperson);
                GUILayout.EndHorizontal();

                GUILayout.Label($"Distance ({settingsData.fl_ThirdpersonDistance})");
                settingsData.fl_ThirdpersonDistance = GUILayout.HorizontalSlider(settingsData.fl_ThirdpersonDistance, 1, 4);
            });

            UI.TabContents("Settings", UI.Tabs.Settings, () =>
            {
                UI.Checkbox(ref settingsData.b_Crosshair, "Crosshair", "Displays a crosshair on the screen.");
                UI.Checkbox(ref settingsData.b_DisplayGroupCredits, "Display Group Credits", "Shows how many credits you have.");
                UI.Checkbox(ref settingsData.b_DisplayQuota, "Display Quota", "Shows the current quota.");
                UI.Checkbox(ref settingsData.b_DisplayDaysLeft, "Display Days Left", "Shows the time you have left to meet quota.");
                UI.Checkbox(ref settingsData.b_CenteredIndicators, "Centered Indicators", "Displays the above indicators at the center of the screen.");
                UI.Checkbox(ref settingsData.b_DeadPlayers, "Dead Player List", "Shows a list of currently dead players.");
                UI.Checkbox(ref settingsData.b_Tooltips, "Tooltips", "Shows information about the currently hovered menu item.");

                UI.Header("Colors");
                UI.ColorPicker("Theme", ref settingsData.c_Theme);
                UI.ColorPicker("Valve", ref settingsData.c_Valve);
                UI.ColorPicker("Enemy", ref settingsData.c_Enemy);
                UI.ColorPicker("Turret", ref settingsData.c_Turret);
                UI.ColorPicker("Landmine", ref settingsData.c_Landmine);
                UI.ColorPicker("Player", ref settingsData.c_Player);
                UI.ColorPicker("Door", ref settingsData.c_Door);
                UI.ColorPicker("Loot", ref settingsData.c_Loot);
                UI.ColorPicker("Small Loot", ref settingsData.c_smallLoot);
                UI.ColorPicker("Medium Loot", ref settingsData.c_medLoot);
                UI.ColorPicker("Big Loot", ref settingsData.c_bigLoot);
            });

            UI.RenderTooltip();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        public static void TeleportAllItems()
        {
            if (Instance != null && HUDManager.Instance != null && Instance.localPlayer != null)
            {
                PlayerControllerB localPlayer = Instance.localPlayer;
                foreach (GrabbableObject grabbableObject in Instance.items)
                {
                    if (!grabbableObject.isHeld && !grabbableObject.isPocketed && !grabbableObject.isInShipRoom)
                    {
                        Vector3 point = new Ray(localPlayer.gameplayCamera.transform.position, localPlayer.gameplayCamera.transform.forward).GetPoint(1f);
                        grabbableObject.gameObject.transform.position = point;
                        grabbableObject.startFallingPosition = point;
                        grabbableObject.targetFloorPosition = point;
                    }
                }
            }

        }

        private void DisplayObjects<T>(IEnumerable<T> objects, bool shouldDisplay, Func<T, string> labelSelector, Func<T, Color> colorSelector) where T : Component
        {
            if (!shouldDisplay) return;

            foreach (T obj in objects)
            {
                if (obj != null && obj.gameObject.activeSelf)
                {
                    float distanceToPlayer = PAUtils.GetDistance(Instance.localPlayer.gameplayCamera.transform.position,
                        obj.transform.position);
                    Vector3 pos;
                    if (PAUtils.WorldToScreen(Features.Thirdperson.ThirdpersonCamera.ViewState ? Features.Thirdperson.ThirdpersonCamera._camera 
                        : Instance.localPlayer.gameplayCamera, obj.transform.position, out pos))
                    {
                        string ObjName = PAUtils.ConvertFirstLetterToUpperCase(labelSelector(obj));
                        if (settingsData.b_DisplayDistance)
                            ObjName += " [" + distanceToPlayer.ToString().ToUpper() + "M]";
                        Render.String(Style, pos.x, pos.y, 150f, 50f, ObjName, colorSelector(obj), true, true);
                    }
                }
            }
        }

        public void DisplayDeadPlayers()
        {
            if (!settingsData.b_DeadPlayers) return;

            float yOffset = 30f;

            foreach (PlayerControllerB playerControllerB in Instance.players)
            {
                if (playerControllerB != null && playerControllerB.isPlayerDead)
                {
                    string strPlayer = playerControllerB.playerUsername;
                    Render.String(Style, 10f, yOffset, 200f, Settings.TEXT_HEIGHT, strPlayer, GUI.color);
                    yOffset += (Settings.TEXT_HEIGHT - 10f);
                }
            }
        }

        private void DisplayShip()
        {
            DisplayObjects(
                new[] { Instance.shipDoor },
                settingsData.b_ShipESP,
                _ => "Ship",
                _ => settingsData.c_Door
            );
        }

        private void DisplayDoors()
        {
            DisplayObjects(
                Instance.entranceTeleports,
                settingsData.b_DoorESP,
                entranceTeleport => entranceTeleport.isEntranceToBuilding ? "Entrance" : "Exit",
                _ => settingsData.c_Door
            );
        }

        private void DisplayLandmines()
        {
            DisplayObjects(
                Instance.landmines.Where(landmine => landmine != null && landmine.IsSpawned && !landmine.hasExploded &&
                    ((settingsData.b_MineDistanceLimit &&
                    PAUtils.GetDistance(Instance.localPlayer.gameplayCamera.transform.position,
                        landmine.transform.position) < settingsData.fl_MineDistanceLimit) ||
                        !settingsData.b_MineDistanceLimit)),
                settingsData.b_LandmineESP,
                _ => "Landmine",
                _ => settingsData.c_Landmine
            );
        }

        private void DisplayTurrets()
        {
            DisplayObjects(
                Instance.turrets.Where(turret => turret != null && turret.IsSpawned &&
                    ((settingsData.b_TurretDistanceLimit &&
                    PAUtils.GetDistance(Instance.localPlayer.gameplayCamera.transform.position,
                        turret.transform.position) < settingsData.fl_TurretDistanceLimit) ||
                        !settingsData.b_TurretDistanceLimit)),
                settingsData.b_TurretESP,
                _ => "Turret",
                _ => settingsData.c_Turret
            );
        }

        private void DisplaySteamHazard()
        {
            DisplayObjects(
                Instance.steamValves.Where(steamValveHazard => steamValveHazard != null && steamValveHazard.triggerScript.interactable),
                settingsData.b_SteamHazard,
                _ => "Steam Valve",
                _ => settingsData.c_Valve
            );
        }

        private void DisplayPlayers()
        {
            DisplayObjects(
                Instance.players.Where(playerControllerB =>
                    IsPlayerValid(playerControllerB) &&
                    !playerControllerB.IsLocalPlayer &&
                     playerControllerB.playerUsername != Instance.localPlayer.playerUsername &&
                    !playerControllerB.isPlayerDead
                ),
                settingsData.b_PlayerESP,
                playerControllerB =>
                {
                    string str = playerControllerB.playerUsername;
                    if (settingsData.b_DisplaySpeaking && playerControllerB.voicePlayerState.IsSpeaking)
                        str += " [VC]";
                    if (settingsData.b_DisplayHP)
                        str += " [" + playerControllerB.health + "HP]";
                    return str;
                },
                _ => settingsData.c_Player
            );
        }

        private void DisplayEnemyAI()
        {
            DisplayObjects(
                Instance.enemies.Where(enemyAI =>
                    enemyAI != null &&
                    enemyAI.eye != null &&
                    enemyAI.enemyType != null &&
                    !enemyAI.isEnemyDead &&
                    ((settingsData.b_EnemyDistanceLimit &&
                    PAUtils.GetDistance(Instance.localPlayer.gameplayCamera.transform.position,
                        enemyAI.transform.position) < settingsData.fl_EnemyDistanceLimit) ||
                        !settingsData.b_EnemyDistanceLimit)
                ),
                settingsData.b_EnemyESP,
                enemyAI =>
                {
                    string name = enemyAI.enemyType.enemyName;
                    return string.IsNullOrWhiteSpace(name) ? "Enemy" : name;
                },
                _ => settingsData.c_Enemy
            );
        }

        private Color GetLootColor(int value)
        {
            if (value <= 15) return settingsData.c_smallLoot;
            if (value > 15 && value <= 35) return settingsData.c_medLoot;
            if (value >= 36) return settingsData.c_bigLoot;
            else return settingsData.c_Loot;
        }

        private void DisplayLoot()
        {
            DisplayObjects(
                Instance.items.Where(grabbableObject =>
                    grabbableObject != null &&
                    !grabbableObject.isHeld &&
                    !grabbableObject.isPocketed &&
                    grabbableObject.itemProperties != null &&
                    ((settingsData.b_ItemDistanceLimit &&
                    PAUtils.GetDistance(Instance.localPlayer.gameplayCamera.transform.position,
                        grabbableObject.transform.position) < settingsData.fl_ItemDistanceLimit) ||
                        !settingsData.b_ItemDistanceLimit)
                ),
                settingsData.b_ItemESP,
                grabbableObject =>
                {
                    string text = "Object";
                    Item itemProperties = grabbableObject.itemProperties;
                    if (itemProperties.itemName != null)
                        text = itemProperties.itemName;
                    int scrapValue = grabbableObject.scrapValue;
                    if (settingsData.b_DisplayWorth && scrapValue > 0)
                        text += " [" + scrapValue.ToString() + "C]";
                    return text;
                },
                grabbableObject => GetLootColor(grabbableObject.scrapValue)
            );
        }

        public bool HasLineOfSightToPosition(Vector3 pos)
        {
            return !Physics.Linecast(Instance.localPlayer.playerEye.transform.position, pos, StartOfRound.Instance.collidersAndRoomMaskAndDefault);
        }

        public void SwitchEnemySnap()
        {
            var enemiesOnScreen = Instance.enemies.Where(enemyAI => enemyAI != null && enemyAI.eye != null && enemyAI.enemyType != null && !enemyAI.isEnemyDead);

            enemySnap++;
            if (enemySnap > enemiesOnScreen.Count())
            {
                enemySnap = 0;
            }

            if (enemiesOnScreen.Count() == 0) enemySnap = -1;
        }

        public void SwitchPlayerSnap()
        {
            var enemiesOnScreen = FindObjectsOfType<PlayerControllerB>().Where(player => player != null && player != Instance.localPlayer).ToArray();

            playerSnap++;
            if (playerSnap > enemiesOnScreen.Count())
            {
                playerSnap = 0;
            }

            if (enemiesOnScreen.Count() == 0) playerSnap = -1;
        }

        public void PlayerTargetUpdate()
        {
            Settings.Instance.settingsData.b_isAimbotting = true;

            var localPlayerDirection = Instance.localPlayer.gameplayCamera.transform.forward;
            var playerObjects = FindObjectsOfType<PlayerControllerB>().Where(player => player != null && player != Instance.localPlayer).ToArray();

            if (!playerObjects.Any()) { Debug.LogError("No player found!"); ResetCam(); return; }

            var closestPlayer = playerObjects.Aggregate((minPlayer, nextPlayer) =>
            {
                Vector3 directionToMinPlayer = minPlayer.gameplayCamera.transform.position - Instance.localPlayer.gameplayCamera.transform.position;
                Vector3 directionToNextPlayer = nextPlayer.gameplayCamera.transform.position - Instance.localPlayer.gameplayCamera.transform.position;
                float angleDifferenceToMinPlayer = Vector3.Angle(localPlayerDirection, directionToMinPlayer);
                float angleDifferenceToNextPlayer = Vector3.Angle(localPlayerDirection, directionToNextPlayer);

                return angleDifferenceToMinPlayer < angleDifferenceToNextPlayer ? minPlayer : nextPlayer;
            });

            if (!snapClosest)
            {
                var enemiesOnScreenList = playerObjects.ToList();
                if (playerSnap >= 0 && playerSnap < enemiesOnScreenList.Count)
                {
                    closestPlayer = enemiesOnScreenList[playerSnap];
                }
            }

            SnapTo(closestPlayer.gameplayCamera.transform.position);

            if (PAUtils.GetAsyncKeyState((int)Keys.E) != 0 && !Instance.localPlayer.isInHangarShipRoom && killTimer > 0.15f)
            {
                killPlayerAimbot(closestPlayer);
            }
        }

        public void AimbotUpdate()
        {
            Settings.Instance.settingsData.b_isAimbotting = true;

            var localPlayerDirection = Instance.localPlayer.gameplayCamera.transform.forward;
            var enemiesOnScreen = Instance.enemies.Where(enemyAI => enemyAI != null && enemyAI.eye != null && enemyAI.enemyType != null && !enemyAI.isEnemyDead);

            if (!enemiesOnScreen.Any()) { Debug.LogError("No enemy found!"); ResetCam(); return; }

            var closestEnemyOnScreen = enemiesOnScreen.Aggregate((minEnemy, nextEnemy) =>
            {
                Vector3 directionToMinEnemy = minEnemy.eye.position - Instance.localPlayer.gameplayCamera.transform.position;
                Vector3 directionToNextEnemy = nextEnemy.eye.position - Instance.localPlayer.gameplayCamera.transform.position;
                float angleDifferenceToMinEnemy = Vector3.Angle(localPlayerDirection, directionToMinEnemy);
                float angleDifferenceToNextEnemy = Vector3.Angle(localPlayerDirection, directionToNextEnemy);

                return angleDifferenceToMinEnemy < angleDifferenceToNextEnemy ? minEnemy : nextEnemy;
            });

            if (!snapClosest)
            {
                var enemiesOnScreenList = enemiesOnScreen.ToList();
                if (enemySnap >= 0 && enemySnap < enemiesOnScreenList.Count)
                {
                    closestEnemyOnScreen = enemiesOnScreenList[enemySnap];
                }
            }

            // Check if the player has a clear line of sight to the enemy
            if (!HasLineOfSightToPosition(closestEnemyOnScreen.eye.transform.position) && settingsData.b_WallCheck)
            {
                return;
            }

            SnapTo(closestEnemyOnScreen.eye.transform.position);

            if (PAUtils.GetAsyncKeyState((int)Keys.E) != 0 && !Instance.localPlayer.isInHangarShipRoom && killTimer > 0.15f)
            {
                killEnemyAimbot(closestEnemyOnScreen);
            }
        }

        public void killPlayerAimbot(PlayerControllerB player) {
            player.DamagePlayerFromOtherClientServerRpc(player.health + 1, new Vector3(900, 900, 900), 0);
        }

        public void killEnemyAimbot(EnemyAI closestEnemyOnScreen)
        {
            killTimer = 0f;
            if (closestEnemyOnScreen.GetComponent<BlobAI>() || closestEnemyOnScreen.GetComponent<ForestGiantAI>() || closestEnemyOnScreen.GetComponent<PufferAI>() || closestEnemyOnScreen.GetComponent<JesterAI>())
            {
                closestEnemyOnScreen.KillEnemyServerRpc(true);
            }
            else
            {
                if (closestEnemyOnScreen.GetComponent<DressGirlAI>())
                {
                    //Cant do anything :(
                }
                else
                {
                    closestEnemyOnScreen.KillEnemyServerRpc(false);
                }
            }
            if (Instance.localPlayer.currentlyHeldObject.itemProperties.itemName != "Shotgun")
            {
                GameObject shottyval = SpawnClientItem("Shotgun");
                ShotgunItem shotty = shottyval.GetComponent<ShotgunItem>();
                shottyval.transform.position = Instance.localPlayer.gameplayCamera.transform.position;
                shottyval.transform.rotation = Instance.localPlayer.gameplayCamera.transform.rotation;
                RoundManager.PlayRandomClip(shotty.gunShootAudio, shotty.gunShootSFX, true, 1f, 1840);
                shotty.gunShootParticle.Play(true);
                Wait(() => { shottyval.transform.position = Vector3.zero; }, 0.5f);
                Wait(() => { FullDestroy(shottyval); }, 2f);
            }
        }

        public void SnapTo(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - Instance.localPlayer.gameplayCamera.transform.position;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // Set the camera rotation directly
            Instance.localPlayer.gameplayCamera.transform.rotation = targetRotation;

            // Calculate the desired mouseY movement based on the y rotation
            float mouseYMovement = -targetRotation.eulerAngles.x; // Adjust as needed

            // Set cursor position to simulate mouse movement
            MouseSimulator.SetCursorPos(0, (int)mouseYMovement);

            Quaternion cameraRotation = Instance.localPlayer.gameplayCamera.transform.rotation;
            Vector3 eulerRotation = cameraRotation.eulerAngles;
            Quaternion desiredRotation = Quaternion.Euler(0f, eulerRotation.y, 0f); // Adjust as needed

            // Set the player's rotation directly
            Instance.localPlayer.transform.rotation = desiredRotation;
        }

        public static List<EnemyType> GetEnemyTypes()
        {
            List<EnemyType> types = new List<EnemyType>();

            if (!StartOfRound.Instance)
            {
                return types;
            }

            Action<List<SpawnableEnemyWithRarity>> processEnemies = (enemies) =>
            {
                foreach (var enemy in enemies)
                {
                    if (!types.Contains(enemy.enemyType))
                    {
                        types.Add(enemy.enemyType);
                    }
                }
            };

            foreach (SelectableLevel selectableLevel in StartOfRound.Instance.levels)
            {
                processEnemies(selectableLevel.Enemies);
                processEnemies(selectableLevel.DaytimeEnemies);
                processEnemies(selectableLevel.OutsideEnemies);
            }

            return types;
        }

        public static object CallMethod(object instance, string methodName, BindingFlags bindingFlags, params object[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, bindingFlags);
            if (method != null)
            {
                return method.Invoke(instance, parameters);
            }
            return null;
        }

        public static void SpawnEnemy(EnemyType type, int num, bool outside, Vector3 spawnPos)
        {
            spawnPos = spawnPos == default ? Instance.localPlayer.transform.position : spawnPos;

            SelectableLevel currentLevel = StartOfRound.Instance.currentLevel;
            PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
            currentLevel.maxEnemyPowerCount = int.MaxValue;
            GameObject[] array = outside ? RoundManager.Instance.outsideAINodes : RoundManager.Instance.insideAINodes;
            for (int i = 0; i < num; i++)
            {
                GameObject gameObject = array[Random.Range(0, array.Length)];
                RoundManager.Instance.SpawnEnemyGameObject(spawnPos, 0f, -1, type);
            }
        }

        private string SpawnEnemyManager(String name, Vector3 spawnPos, int count = 1, bool outside = false)
        {
            if (!Instance.localPlayer.IsHost) return "Not host";

            AllItemsList allItemsList = StartOfRound.Instance.allItemsList;
            float closestMatchScore = float.MaxValue;
            string closestMatch = null;

            foreach (EnemyType enemyType in GetEnemyTypes())
            {
                string currentItemName = enemyType.enemyName;
                int matchScore = LevenshteinDistance.Calculate(name, currentItemName);

                if (matchScore < closestMatchScore)
                {
                    closestMatchScore = matchScore;
                    closestMatch = currentItemName;
                }

                if (enemyType.enemyName == name)
                {

                    try
                    {
                        SpawnEnemy(enemyType, count, outside, spawnPos);
                        return "true";
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.ToString());
                        return closestMatch;
                    }
                }
            }

            // Log the closest match before returning false
            Debug.Log($"No exact match found for '{name}'. Closest match: '{closestMatch}'");

            /* Make autocorrect spawn here */
            return closestMatch;

        }

        private void TriggerButton(InteractTrigger trigger)
        {
            trigger.Interact(((Component)GameNetworkManager.Instance.localPlayerController).transform);
        }

        public void ResetCam()
        {
            Instance.localPlayer.gameplayCamera.transform.rotation = Instance.localPlayer.transform.rotation;
        }

        public void Start()
        {
            Harmony harmony = new Harmony("com.waxxyTF2.ProjectApparatus");
            harmony.PatchAll();

            StartCoroutine(Instance.CollectObjects());

            Settings.Changelog.ReadChanges();
            Settings.Credits.ReadCredits();

            Settings.Instance.settingsData.b_FreeShit = false;
        }

        public void SwitchCam()
        {
            settingsData.switchingCam = true;
            GameObject val5 = GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/Cube.001/CameraMonitorSwitchButton/Cube (2)");
            if ((Object)(object)val5 != (Object)null)
            {
                TriggerButton(val5.GetComponent<InteractTrigger>());
            }
            settingsData.switchingCam = false;
        }

        public void CreateTopRightCamera()
        {
            if (topRightCamera) { Debug.LogError("Already exists..."); return; }
            topRightCamera = true;
            // Create a new camera
            cameraObject = new GameObject("TopRightCamera");
            Camera camera = cameraObject.AddComponent<Camera>();

            // Configure the camera
            camera.clearFlags = CameraClearFlags.Depth;
            camera.depth = Mathf.Infinity; // High depth to display on top of everything else

            // Position the camera at the top right of the player's screen
            Vector3 playerPosition = Instance.localPlayer.gameplayCamera.transform.position;
            cameraObject.transform.position = new Vector3(Instance.localPlayer.gameplayCamera.transform.position.x, Instance.localPlayer.gameplayCamera.transform.position.y + cameraOffsetY, Instance.localPlayer.gameplayCamera.transform.position.z);

            // Rotate the camera to face the player
            cameraObject.transform.LookAt(Instance.localPlayer.gameplayCamera.transform.position);

            //Show layers only the player can see
            camera.cullingMask = Instance.localPlayer.gameplayCamera.cullingMask;

            // Set the rect property to define the area of the screen that the camera renders to
            camera.rect = new Rect(0.75f, 0.75f, 0.25f, 0.25f); // This will cover the top right corner of the screen
        }

        public void CreateMonitorCamera()
        {
            if (monitorCamera) { Debug.LogError("Already exists..."); return; }

            monitorCamera = true;
            // Create a new camera
            monitorCameraObject = new GameObject("TopRightCamera");
            Camera camera = monitorCameraObject.AddComponent<Camera>();

            // Configure the camera
            camera.clearFlags = CameraClearFlags.Depth;
            camera.depth = Mathf.Infinity; // High depth to display on top of everything else

            // Position the camera at the top right of the player's screen
            Vector3 playerPosition = Instance.localPlayer.gameplayCamera.transform.position;
            monitorCameraObject.transform.position = new Vector3((float)9.5, (float)2.62, (float)-14.5);

            // Rotate the camera to face the player
            monitorCameraObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            //Show layers only the player can see
            camera.cullingMask = Instance.localPlayer.gameplayCamera.cullingMask;

            // Set the rect property to define the area of the screen that the camera renders to
            camera.rect = new Rect(0f, 0.75f, 0.25f, 0.25f); // This will cover the top left corner of the screen
        }

        public void DeleteTopCam()
        {
            Destroy(cameraObject);
            topRightCamera = false;
        }

        public void DeleteMonitorCam()
        {
            Destroy(monitorCameraObject);
            monitorCamera = false;
        }
        public void TopRightCameraUpdate()
        {
            cameraObject.transform.position = new Vector3(Instance.localPlayer.gameplayCamera.transform.position.x, Instance.localPlayer.gameplayCamera.transform.position.y + cameraOffsetY, Instance.localPlayer.gameplayCamera.transform.position.z);

            // Check the state of the Up and Down arrow keys
            short upDown = PAUtils.GetAsyncKeyState((int)Keys.Up);
            short downDown = PAUtils.GetAsyncKeyState((int)Keys.Down);

            // Convert the short values to bool
            bool upIsDown = upDown < 0;
            bool downIsDown = downDown < 0;

            // If the Up arrow key is down and hasn't been pressed yet in the current frame,
            // increment the variable and set upPressed to true
            if (upIsDown && !upPressed)
            {
                cameraOffsetY++;
                upPressed = true;
            }
            // If the Up arrow key is not down, reset upPressed to false
            else if (!upIsDown)
            {
                upPressed = false;
            }

            // Do the same for the Down arrow key
            if (downIsDown && !downPressed)
            {
                cameraOffsetY--;
                downPressed = true;
            }
            else if (!downIsDown)
            {
                downPressed = false;
            }
        }

        private void BtnTeleport()
        {

                for (int i = 0; i < StartOfRound.Instance.unlockablesList.unlockables.Count; i++)
                {
                    if (StartOfRound.Instance.unlockablesList.unlockables[i].unlockableName == "Teleporter")
                    {
                        if (Enum.IsDefined(typeof(UnlockableUpgrade), i) &&
                            !StartOfRound.Instance.unlockablesList.unlockables[i].hasBeenUnlockedByPlayer)
                        {
                            StartOfRound.Instance.BuyShipUnlockableServerRpc(i, Instance.shipTerminal.groupCredits);
                            StartOfRound.Instance.SyncShipUnlockablesServerRpc();
                        }
                    }
                }

            GameObject val4 = GameObject.Find("Teleporter(Clone)/ButtonContainer/ButtonAnimContainer/RedButton");

            for (int i = 0; i < StartOfRound.Instance.unlockablesList.unlockables.Count; i++)
            {
                if (StartOfRound.Instance.unlockablesList.unlockables[i].unlockableName == "Teleporter")
                {
                    if (Enum.IsDefined(typeof(UnlockableUpgrade), i) &&
                        !StartOfRound.Instance.unlockablesList.unlockables[i].hasBeenUnlockedByPlayer)
                    {
                        StartOfRound.Instance.BuyShipUnlockableServerRpc(i, Instance.shipTerminal.groupCredits);
                        StartOfRound.Instance.SyncShipUnlockablesServerRpc();
                    }
                }
            }

            if ((Object)(object)val4 != null)
            {
                TriggerButton(val4.GetComponent<InteractTrigger>());
            }
        }

        private void BtnInverseTeleport()
        {

            for (int i = 0; i < StartOfRound.Instance.unlockablesList.unlockables.Count; i++)
            {
                if (StartOfRound.Instance.unlockablesList.unlockables[i].unlockableName == "InverseTeleporter")
                {
                    if (Enum.IsDefined(typeof(UnlockableUpgrade), i) &&
                        !StartOfRound.Instance.unlockablesList.unlockables[i].hasBeenUnlockedByPlayer)
                    {
                        StartOfRound.Instance.BuyShipUnlockableServerRpc(i, Instance.shipTerminal.groupCredits);
                        StartOfRound.Instance.SyncShipUnlockablesServerRpc();
                    }
                }
            }

            GameObject val3 = GameObject.Find("InverseTeleporter/ButtonContainer/ButtonAnimContainer/RedButton");

            for (int i = 0; i < StartOfRound.Instance.unlockablesList.unlockables.Count; i++)
            {
                if (StartOfRound.Instance.unlockablesList.unlockables[i].unlockableName == "InverseTeleporter")
                {
                    if (Enum.IsDefined(typeof(UnlockableUpgrade), i) &&
                        !StartOfRound.Instance.unlockablesList.unlockables[i].hasBeenUnlockedByPlayer)
                    {
                        StartOfRound.Instance.BuyShipUnlockableServerRpc(i, Instance.shipTerminal.groupCredits);
                        StartOfRound.Instance.SyncShipUnlockablesServerRpc();
                    }
                }
            }

            if ((Object)(object)val3 != null)
            {
                TriggerButton(val3.GetComponent<InteractTrigger>());
            }
        }

        public void CooldownCheck()
        {
            if (inverse == null || teleporter == null)
            {
                teleporter = GameObject.Find("Teleporter(Clone)");
                inverse = GameObject.Find("InverseTeleporter(Clone)");
            }
            if (inverse != null)
            {
                ShipTeleporter inverseTeleporter = inverse.GetComponent<ShipTeleporter>();

                if (inverseTeleporter != null)
                {
                    if (settingsData.b_noButtonCooldown)
                    {
                        inverseTeleporter.cooldownAmount = 0f;
                    }
                    else
                    {
                        inverseTeleporter.cooldownAmount = 300f;
                    }
                } else
                {
                    Debug.LogError("No inverse teleporter script..");
                }
            }
            if (teleporter != null) {

                ShipTeleporter shipTeleporter = teleporter.GetComponent<ShipTeleporter>();
                if (shipTeleporter != null)
                {
                    if (settingsData.b_noButtonCooldown)
                    {
                        shipTeleporter.cooldownAmount = 0f;
                    }
                    else
                    {
                        shipTeleporter.cooldownAmount = 9f;
                    }
                }
            } else
            {
                Debug.LogError("No teleporter script..");
            }

        }

        public string SpawnItem(string name)
        {
            if (!Instance.localPlayer.IsHost) return "Not host";

            AllItemsList allItemsList = StartOfRound.Instance.allItemsList;
            float closestMatchScore = float.MaxValue;
            string closestMatch = null;

            for (int i = 0; i < allItemsList.itemsList.Count; i++)
            {
                string currentItemName = allItemsList.itemsList[i].itemName;
                int matchScore = LevenshteinDistance.Calculate(name, currentItemName);

                if (matchScore < closestMatchScore)
                {
                    closestMatchScore = matchScore;
                    closestMatch = currentItemName;
                }

                if (currentItemName == name)
                {
                    try
                    {
                        GameObject val = Object.Instantiate<GameObject>(StartOfRound.Instance.allItemsList.itemsList[i].spawnPrefab, Instance.localPlayer.transform.position, Quaternion.identity);
                        val.GetComponent<GrabbableObject>().fallTime = 0f;
                        val.AddComponent<ScanNodeProperties>().scrapValue = StartOfRound.Instance.allItemsList.itemsList[i].maxValue;
                        val.GetComponent<GrabbableObject>().SetScrapValue(StartOfRound.Instance.allItemsList.itemsList[i].maxValue);
                        val.GetComponent<NetworkObject>().Spawn(false);
                        return "true";
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.ToString());
                        return closestMatch;
                    }
                }
            }

            // Log the closest match before returning false
            Debug.Log($"No exact match found for '{name}'. Closest match: '{closestMatch}'");

            /* Make autocorrect spawn here */
            return closestMatch;
        }

        public GameObject SpawnClientItem(string name)
        {

            AllItemsList allItemsList = StartOfRound.Instance.allItemsList;
            float closestMatchScore = float.MaxValue;
            string closestMatch = null;

            for (int i = 0; i < allItemsList.itemsList.Count; i++)
            {
                string currentItemName = allItemsList.itemsList[i].itemName;
                int matchScore = LevenshteinDistance.Calculate(name, currentItemName);

                if (matchScore < closestMatchScore)
                {
                    closestMatchScore = matchScore;
                    closestMatch = currentItemName;
                }

                if (currentItemName == name)
                {
                        GameObject val = Object.Instantiate<GameObject>(StartOfRound.Instance.allItemsList.itemsList[i].spawnPrefab, Instance.localPlayer.transform.position, Quaternion.identity);
                        val.GetComponent<GrabbableObject>().fallTime = 0f;
                        val.AddComponent<ScanNodeProperties>().scrapValue = StartOfRound.Instance.allItemsList.itemsList[i].maxValue;
                        val.AddComponent<ShotgunItem>();
                        return val;
                }
            }
            return new GameObject();
        }

        public void FullDestroy(GameObject val)
        {
            MeshRenderer[] componentsInChildren = val.GetComponentsInChildren<MeshRenderer>();
            for (int i = 0; i < componentsInChildren.Length; i++)
            {
                Object.Destroy(componentsInChildren[i]);
            }
            Collider[] componentsInChildren2 = val.GetComponentsInChildren<Collider>();
            for (int j = 0; j < componentsInChildren2.Length; j++)
            {
                Object.Destroy(componentsInChildren2[j]);
            }
            try
            {
                Destroy(val);
            } catch(Exception e)
            {
            }
        }

        public void SetItemValue(float value)
        {
            //Get currently held item here
            GrabbableObject val = Instance.localPlayer.currentlyHeldObject;
            val.GetComponent<GrabbableObject>().SetScrapValue((int)value);
        }

        public void DoomMode()
        {
            settingsData.b_AimbotEnabled = true;
            settingsData.b_WallCheck = true;
            SpawnItem("Shotgun");
        }

        public void Update()
        {
            killTimer += Time.deltaTime;

            CooldownCheck();

            if (PAUtils.GetAsyncKeyState((int)Keys.LButton) == 0 && settingsData.holdingMouse) settingsData.holdingMouse = false;

            if (PAUtils.GetAsyncKeyState((int)Keys.LButton) == 0 && settingsData.holdingMouseAC) settingsData.holdingMouseAC = false;

                if ((PAUtils.GetAsyncKeyState((int)Keys.Insert) & 1) != 0)
            {
                Settings.Instance.SaveSettings();
                Settings.Instance.b_isMenuOpen = !Settings.Instance.b_isMenuOpen;
            }
            if ((PAUtils.GetAsyncKeyState((int)Keys.Delete) & 1) != 0)
            {
                Loader.Unload();
                StopCoroutine(Instance.CollectObjects());
            }

            if (settingsData.b_AlwaysShowClock && HUDManager.Instance)
            {
                HUDManager.Instance.SetClockVisible(true);
            }

            if (settingsData.b_LightShow)
            {
                if (Instance.shipLights)
                    Instance.shipLights.SetShipLightsServerRpc(!Instance.shipLights.areLightsOn);

                if (Instance.tvScript)
                {
                    if (Instance.tvScript.tvOn)
                        Instance.tvScript.TurnOffTVServerRpc();
                    else
                        Instance.tvScript.TurnOnTVServerRpc();
                }
            }

            if (Instance.shipTerminal)
            {
                if (settingsData.b_NoMoreCredits)
                    Instance.shipTerminal.groupCredits = 0;

                if (settingsData.b_TerminalNoisemaker)
                    Instance.shipTerminal.PlayTerminalAudioServerRpc(1);
            }

            Features.Possession.UpdatePossession();
            Features.Misc.Noclip();

            if (settingsData.b_RemoveVisor) 
                Instance.localVisor?.SetActive(false);

            if (settingsData.b_AnonChatSpam)
                PAUtils.SendChatMessage(settingsData.str_ChatMessage);

            if (settingsData.b_AimbotEnabled && PAUtils.GetAsyncKeyState((int)Keys.RButton) == 0) 
            {
                settingsData.b_isAimbotting = false;
                snapClosest = true;
            }
            else
            {
                if (!settingsData.holdingMouseAC)
                {
                    settingsData.b_isAimbotting = true;
                    if (settingsData.str_AimbotMethod == "Enemy") AimbotUpdate();
                    if (settingsData.str_AimbotMethod == "Player") PlayerTargetUpdate();
                    settingsData.holdingMouseAC = true;
                }
            }

            if(settingsData.b_isAimbotting && PAUtils.GetAsyncKeyState((int)Keys.LButton) != 0)
            {
                if (settingsData.str_AimbotMethod == "Enemy")
                {
                    SwitchEnemySnap();
                } else
                {
                    SwitchPlayerSnap();
                }
                snapClosest = false;
            }

            if (cameraObject==null) topRightCamera = false;
            if (topRightCamera) TopRightCameraUpdate();

            //Testing, log coords
            if ((PAUtils.GetAsyncKeyState((int)Keys.Home) & 1) != 0)
            {
                Vector3 coords = Instance.localPlayer.gameplayCamera.transform.position;
                Debug.LogError("X: " + coords.x + " Y: " + coords.y + " Z: " + coords.z);
            }
        }


        public void Wait(Action callback, float delayInSeconds)
        {
            StartCoroutine(ExecuteAfterDelay(callback, delayInSeconds));
        }

        private IEnumerator ExecuteAfterDelay(Action callback, float delayInSeconds)
        {
            yield return new WaitForSeconds(delayInSeconds);
            callback?.Invoke();
        }

        private Vector2 scrollPos;
        private static Hacks instance;

        public int enemySnap;
        public int playerSnap;
        public bool snapClosest = true;
        public float killTimer = 0f;
    }
}
