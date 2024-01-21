using System;
using System.Diagnostics;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using static UnityEngine.Rendering.DebugUI;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.UI;
using System.Reflection.Emit;
using Debug = UnityEngine.Debug;
using System.ComponentModel;
using static UnityEngine.InputSystem.InputAction;
using System.Windows.Forms;
using Screen = UnityEngine.Screen;
using static Unity.Netcode.NetworkManager;

namespace ProjectApparatus
{
    [HarmonyPatch(typeof(PlayerControllerB), "Start")]
    public class PlayerControllerB_Start_Patch
    {
        private static RenderTexture oTexture = null;
        private static void Prefix(PlayerControllerB __instance)
        {
            __instance.gameplayCamera.targetTexture.width = Settings.Instance.settingsData.b_CameraResolution ? Screen.width : 860;
            __instance.gameplayCamera.targetTexture.height = Settings.Instance.settingsData.b_CameraResolution ? Screen.height : 520;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    public class PlayerControllerB_Update_Patch
    {
        private static float oWeight = 1f;
        private static float oFOV = 66f;
        public static bool Prefix(PlayerControllerB __instance)
        {
            if (__instance == GameObjectManager.Instance.localPlayer)
            {
                oFOV = __instance.gameplayCamera.fieldOfView;

                __instance.disableLookInput = (__instance.quickMenuManager.isMenuOpen || Settings.Instance.b_isMenuOpen) ? true : false;
                UnityEngine.Cursor.visible = (__instance.quickMenuManager.isMenuOpen || Settings.Instance.b_isMenuOpen) ? true : false;
                UnityEngine.Cursor.lockState = (__instance.quickMenuManager.isMenuOpen || Settings.Instance.b_isMenuOpen) ? CursorLockMode.None : CursorLockMode.Locked;

                oWeight = __instance.carryWeight;
                if (Settings.Instance.settingsData.b_RemoveWeight)
                    __instance.carryWeight = 1f;
            }

            return true;
        }

        public static void Postfix(PlayerControllerB __instance)
        {
            if (__instance == GameObjectManager.Instance.localPlayer)
            {
                __instance.carryWeight = oWeight; // Restore weight after the speed has been calculated @ float num3 = this.movementSpeed / this.carryWeight;

                float flTargetFOV = Settings.Instance.settingsData.i_FieldofView;

                flTargetFOV = __instance.inTerminalMenu ? flTargetFOV - 6f :
                             (__instance.IsInspectingItem ? flTargetFOV - 20f :
                             (__instance.isSprinting ? flTargetFOV + 2f : flTargetFOV));


                __instance.gameplayCamera.fieldOfView = Mathf.Lerp(oFOV, flTargetFOV, 6f * Time.deltaTime);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
    public class PlayerControllerB_LateUpdate_Patch
    {
        private static float ojumpForce = 0f,
            minIntensity = 100f,
            maxIntensity = 10000f;

        public static void Postfix(PlayerControllerB __instance)
        {
            if (!__instance || !StartOfRound.Instance)
                return;

            if (Settings.Instance.b_DemiGod.ContainsKey(__instance) && Settings.Instance.b_DemiGod[__instance] && __instance.health < 100)
                __instance.DamagePlayerFromOtherClientServerRpc(-(100 - __instance.health), new Vector3(0, 0, 0), 0);

            if (Settings.Instance.b_SpamObjects.ContainsKey(__instance) && Settings.Instance.b_SpamObjects[__instance]
                && GameObjectManager.Instance.shipBuildModeManager)
            {
                foreach (PlaceableShipObject shipObject in GameObjectManager.Instance.shipObjects)
                {
                    NetworkObject networkObject = shipObject.parentObject.GetComponent<NetworkObject>();
                    if (StartOfRound.Instance.unlockablesList.unlockables[shipObject.unlockableID].inStorage)
                        StartOfRound.Instance.ReturnUnlockableFromStorageServerRpc(shipObject.unlockableID);

                    GameObjectManager.Instance.shipBuildModeManager.PlaceShipObject(__instance.transform.position,
                        __instance.transform.eulerAngles,
                        shipObject);
                    GameObjectManager.Instance.shipBuildModeManager.CancelBuildMode(false);
                    GameObjectManager.Instance.shipBuildModeManager.PlaceShipObjectServerRpc(__instance.transform.position,
                        shipObject.mainMesh.transform.eulerAngles,
                        networkObject,
                        Settings.Instance.b_HideObjects ? (int)__instance.playerClientId : -1);
                }
            }

            if (Settings.Instance.b_SpamChat.ContainsKey(__instance) && Settings.Instance.b_SpamChat[__instance])
                PAUtils.SendChatMessage(Settings.Instance.str_ChatAsPlayer, (int)__instance.playerClientId);

            if (Settings.Instance.settingsData.b_AllJetpacksExplode)
            {
                if (__instance.currentlyHeldObjectServer != null && __instance.currentlyHeldObjectServer.GetType() == typeof(JetpackItem))
                {
                    JetpackItem Jetpack = (__instance.currentlyHeldObjectServer as JetpackItem);// fill it in
                    if (Jetpack != null)
                    {
                        PAUtils.SetValue(__instance, "jetpackPower", float.MaxValue, PAUtils.protectedFlags);
                        PAUtils.CallMethod(__instance, "ActivateJetpack", PAUtils.protectedFlags, null);
                        Jetpack.ExplodeJetpackServerRpc();
                        Jetpack.ExplodeJetpackClientRpc();
                    }
                }
            }

            PlayerControllerB Local = GameObjectManager.Instance.localPlayer;
            if (__instance.actualClientId != Local.actualClientId)
                return;

            if (Settings.Instance.settingsData.b_InfiniteStam)
            {
                __instance.sprintMeter = 1f;
                if (__instance.sprintMeterUI != null)
                    __instance.sprintMeterUI.fillAmount = 1f;
            }

            if (Settings.Instance.settingsData.b_InfiniteCharge)
            {
                if (__instance.currentlyHeldObjectServer != null
                    && __instance.currentlyHeldObjectServer.insertedBattery != null)
                {
                    __instance.currentlyHeldObjectServer.insertedBattery.empty = false;
                    __instance.currentlyHeldObjectServer.insertedBattery.charge = 1f;
                }
            }

            if (__instance.currentlyHeldObjectServer != null)
            {
                if (Settings.Instance.settingsData.b_ChargeAnyItem)
                    __instance.currentlyHeldObjectServer.itemProperties.requiresBattery = true;

                if (Settings.Instance.settingsData.b_OneHandAllObjects)
                {
                    __instance.twoHanded = false;
                    __instance.currentlyHeldObjectServer.itemProperties.twoHanded = false;
                }
                //Made seperate cause some objects like stop signs wont animate if you turn off two handed animation.
                if (Settings.Instance.settingsData.b_OneHandAnimations)
                {
                    __instance.twoHandedAnimation = false;
                    __instance.currentlyHeldObjectServer.itemProperties.twoHandedAnimation = false;
                }
            }

            if (Settings.Instance.settingsData.b_WalkSpeed && !__instance.isSprinting)
                PAUtils.SetValue(__instance, "sprintMultiplier", Settings.Instance.settingsData.i_WalkSpeed, PAUtils.protectedFlags);

            if (Settings.Instance.settingsData.b_SprintSpeed && __instance.isSprinting)
                PAUtils.SetValue(__instance, "sprintMultiplier", Settings.Instance.settingsData.i_SprintSpeed, PAUtils.protectedFlags);

            __instance.climbSpeed = (Settings.Instance.settingsData.b_FastLadderClimbing) ? 100f : 4f;

            BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PAUtils.SetValue(__instance, "interactableObjectsMask",
                Settings.Instance.settingsData.b_InteractThroughWalls ? LayerMask.GetMask(new string[] { "Props", "InteractableObject" }) : 832,
                bindingAttr);

            __instance.grabDistance = Settings.Instance.settingsData.b_UnlimitedGrabDistance ? 9999f : 5f;

            if (ojumpForce == 0f)
                ojumpForce = __instance.jumpForce;
            else
                __instance.jumpForce = Settings.Instance.settingsData.b_JumpHeight ? Settings.Instance.settingsData.i_JumpHeight : ojumpForce;

            if (__instance.nightVision)
            {
                /* I see a lot of cheats set nightVision.enabled to false when the feature is off, this is wrong as the game sets it to true when you're in-doors. 
                   Also there's no reason to reset it as the game automatically sets it back every time Update is called. */

                if (Settings.Instance.settingsData.b_NightVision)
                    __instance.nightVision.enabled = true;

                __instance.nightVision.range = (Settings.Instance.settingsData.b_NightVision) ? 9999f : 12f;
                __instance.nightVision.intensity = (minIntensity + (maxIntensity - minIntensity) * (Settings.Instance.settingsData.i_NightVision / 100f));
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "Jump_performed")]
    public class Jump_performed_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerControllerB __instance)
        {
            bool orig = Settings.Instance.settingsData.b_SprintSpeed;
            Settings.Instance.settingsData.b_SprintSpeed = false;
            if (Settings.Instance.settingsData.b_bhop && __instance.isSprinting)
            {
                PAUtils.SetValue(__instance, "sprintMultiplier", Settings.Instance.settingsData.i_bhopSprint, PAUtils.protectedFlags);
            }

            Settings.Instance.settingsData.b_SprintSpeed = orig;
            return true;
        }
    }

    /*
    [HarmonyPatch(typeof(GameNetworkManager), "LeaveLobbyAtGameStart")]
    [HarmonyWrapSafe]
    internal static class LeaveLobbyAtGameStart_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }
    [HarmonyPatch(typeof(GameNetworkManager), "ConnectionApproval")]
    [HarmonyWrapSafe]
    internal static class ConnectionApproval_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ConnectionApprovalRequest request, ref ConnectionApprovalResponse response)
        {
            if (request.ClientNetworkId != NetworkManager.Singleton.LocalClientId && Plugin.LobbyJoinable && response.Reason == "Game has already started!")
            {
                response.Reason = "";
                response.Approved = true;
            }
        }
    }
    [HarmonyPatch(typeof(QuickMenuManager), "DisableInviteFriendsButton")]
    internal static class DisableInviteFriendsButton_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }
    [HarmonyPatch(typeof(QuickMenuManager), "InviteFriendsButton")]
    internal static class InviteFriendsButton_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (Plugin.LobbyJoinable)
            {
                GameNetworkManager.Instance.InviteFriendsUI();
            }
            return false;
        }
    }
    internal class RpcEnum : NetworkBehaviour
    {
        public static int None => 0;

        public static int Client => 2;

        public static int Server => 1;
    }
    internal static class WeatherSync
    {
        public static bool DoOverride = false;

        public static LevelWeatherType CurrentWeather = (LevelWeatherType)(-1);
    }
    [HarmonyPatch(typeof(RoundManager), "__rpc_handler_1193916134")]
    [HarmonyWrapSafe]
    internal static class __rpc_handler_1193916134_Patch
    {
        public static FieldInfo RPCExecStage = typeof(NetworkBehaviour).GetField("__rpc_exec_stage", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPrefix]
        private static bool Prefix(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            //IL_002e: Unknown result type (might be due to invalid IL or missing references)
            //IL_0037: Unknown result type (might be due to invalid IL or missing references)
            //IL_0057: Unknown result type (might be due to invalid IL or missing references)
            //IL_0082: Unknown result type (might be due to invalid IL or missing references)
            NetworkManager networkManager = target.NetworkManager;
            if ((Object)(object)networkManager != (Object)null && networkManager.IsListening && !networkManager.IsHost)
            {
                try
                {
                    int num = default(int);
                    ByteUnpacker.ReadValueBitPacked(reader, ref num);
                    int num2 = default(int);
                    ByteUnpacker.ReadValueBitPacked(reader, ref num2);
                    if (((FastBufferReader)(ref reader)).Position < ((FastBufferReader)(ref reader)).Length)
                    {
                        int num3 = default(int);
                        ByteUnpacker.ReadValueBitPacked(reader, ref num3);
                        num3 -= 255;
                        if (num3 < 0)
                        {
                            throw new Exception("In case of emergency, break glass.");
                        }
                        WeatherSync.CurrentWeather = (LevelWeatherType)num3;
                        WeatherSync.DoOverride = true;
                    }
                    RPCExecStage.SetValue(target, RpcEnum.Client);
                    ((RoundManager)((target is RoundManager) ? target : null)).GenerateNewLevelClientRpc(num, num2);
                    RPCExecStage.SetValue(target, RpcEnum.None);
                    return false;
                }
                catch
                {
                    WeatherSync.DoOverride = false;
                    ((FastBufferReader)(ref reader)).Seek(0);
                    return true;
                }
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(RoundManager), "SetToCurrentLevelWeather")]
    internal static class SetToCurrentLevelWeather_Patch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            //IL_0019: Unknown result type (might be due to invalid IL or missing references)
            //IL_001e: Unknown result type (might be due to invalid IL or missing references)
            if (WeatherSync.DoOverride)
            {
                RoundManager.Instance.currentLevel.currentWeather = WeatherSync.CurrentWeather;
                WeatherSync.DoOverride = false;
            }
        }
    }
    [HarmonyPatch(typeof(StartOfRound), "OnPlayerConnectedClientRpc")]
    [HarmonyWrapSafe]
    internal static class OnPlayerConnectedClientRpc_Patch
    {
        public static MethodInfo BeginSendClientRpc = typeof(RoundManager).GetMethod("__beginSendClientRpc", BindingFlags.Instance | BindingFlags.NonPublic);

        public static MethodInfo EndSendClientRpc = typeof(RoundManager).GetMethod("__endSendClientRpc", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPostfix]
        private static void Postfix(ulong clientId, int connectedPlayers, ulong[] connectedPlayerIdsOrdered, int assignedPlayerObjectId, int serverMoneyAmount, int levelID, int profitQuota, int timeUntilDeadline, int quotaFulfilled, int randomSeed)
        {
            StartOfRound instance = StartOfRound.Instance;
            PlayerControllerB val = instance.allPlayerScripts[assignedPlayerObjectId];
            if (instance.connectedPlayersAmount + 1 >= instance.allPlayerScripts.Length)
            {
                Plugin.SetLobbyJoinable(joinable: false);
            }
            val.DisablePlayerModel(instance.allPlayerObjects[assignedPlayerObjectId], true, true);
            if (((NetworkBehaviour)instance).IsServer && !instance.inShipPhase)
            {
                RoundManager instance2 = RoundManager.Instance;
                ClientRpcParams val2 = default(ClientRpcParams);
                val2.Send = new ClientRpcSendParams
                {
                    TargetClientIds = new List<ulong> { clientId }
                };
                ClientRpcParams val3 = val2;
                FastBufferWriter val4 = (FastBufferWriter)BeginSendClientRpc.Invoke(instance2, new object[3] { 1193916134u, val3, 0 });
                BytePacker.WriteValueBitPacked(val4, StartOfRound.Instance.randomMapSeed);
                BytePacker.WriteValueBitPacked(val4, StartOfRound.Instance.currentLevelID);
                BytePacker.WriteValueBitPacked(val4, instance2.currentLevel.currentWeather + 255);
                EndSendClientRpc.Invoke(instance2, new object[4] { val4, 1193916134u, val3, 0 });
                FastBufferWriter val5 = (FastBufferWriter)BeginSendClientRpc.Invoke(instance2, new object[3] { 2729232387u, val3, 0 });
                EndSendClientRpc.Invoke(instance2, new object[4] { val5, 2729232387u, val3, 0 });
            }
            instance.livingPlayers = instance.connectedPlayersAmount + 1;
            for (int i = 0; i < instance.allPlayerScripts.Length; i++)
            {
                PlayerControllerB val6 = instance.allPlayerScripts[i];
                if (val6.isPlayerControlled && val6.isPlayerDead)
                {
                    instance.livingPlayers--;
                }
            }
        }
    }
    [HarmonyPatch(typeof(StartOfRound), "OnPlayerDC")]
    [HarmonyWrapSafe]
    internal static class OnPlayerDC_Patch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (StartOfRound.Instance.inShipPhase || (Plugin.AllowJoiningWhileLanded && StartOfRound.Instance.shipHasLanded))
            {
                Plugin.SetLobbyJoinable(joinable: true);
            }
        }
    }
    [HarmonyPatch(typeof(StartOfRound), "SetShipReadyToLand")]
    internal static class SetShipReadyToLand_Patch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (StartOfRound.Instance.connectedPlayersAmount + 1 < StartOfRound.Instance.allPlayerScripts.Length)
            {
                Plugin.SetLobbyJoinable(joinable: true);
            }
        }
    }
    [HarmonyPatch(typeof(StartOfRound), "StartGame")]
    internal static class StartGame_Patch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            Plugin.SetLobbyJoinable(joinable: false);
        }
    }
    [HarmonyPatch(typeof(StartOfRound), "OnShipLandedMiscEvents")]
    internal static class OnShipLandedMiscEvents_Patch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (Plugin.AllowJoiningWhileLanded && StartOfRound.Instance.connectedPlayersAmount + 1 < StartOfRound.Instance.allPlayerScripts.Length)
            {
                Plugin.SetLobbyJoinable(joinable: true);
            }
        }
    }
    [HarmonyPatch(typeof(StartOfRound), "ShipLeave")]
    internal static class ShipLeave_Patch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            Plugin.SetLobbyJoinable(joinable: false);
        }
    }
    */

    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    public class Jump_performed_Patch_Speed
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerControllerB __instance)
        {
            if (Settings.Instance.settingsData.b_bhop && __instance.isGroundedOnServer)
            {
                PAUtils.SetValue(__instance, "sprintMultiplier", 1f, PAUtils.protectedFlags);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "PlayerHitGroundEffects")]
    public class PlayerControllerB_PlayerHitGroundEffects_Patch
    {
        public static bool Prefix(PlayerControllerB __instance)
        {
            if (__instance.actualClientId == GameObjectManager.Instance.localPlayer.actualClientId
                && Settings.Instance.settingsData.b_DisableFallDamage)
                __instance.takingFallDamage = false;

            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "AllowPlayerDeath")]
    public class PlayerControllerB_AllowPlayerDeath_Patch
    {
        public static bool Prefix(PlayerControllerB __instance, ref bool __result)
        {
            if ((Settings.Instance.settingsData.b_GodMode || Features.Possession.possessedEnemy != null)
                && __instance == GameObjectManager.Instance.localPlayer)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "CheckConditionsForEmote")]
    public class PlayerControllerB_CheckConditionsForEmote_Patch
    {
        public static bool Prefix(PlayerControllerB __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_TauntSlide)
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "SpectateNextPlayer")]
    public class SpectateNextPlayer_Patch
    {
        public static bool Prefix()
        {
            //Dont spectate next player if menu is open
            if(Settings.Instance.b_isMenuOpen)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Landmine), "Update")]
    public class Landmine_Update_Patch
    {
        public static bool Prefix(Landmine __instance)
        {
            if (Settings.Instance.settingsData.b_SensitiveLandmines && !__instance.hasExploded)
            {
                foreach (PlayerControllerB plyr in GameObjectManager.Instance.players)
                {
                    if (plyr.actualClientId == GameObjectManager.Instance.localPlayer.actualClientId) continue;

                    Vector3 plyrPosition = plyr.transform.position,
                        minePosition = __instance.transform.position;

                    float distance = Vector3.Distance(plyrPosition, minePosition);
                    if (distance <= 4f)
                        __instance.ExplodeMineServerRpc();
                }
            }

            if (Settings.Instance.settingsData.b_LandmineEarrape)
                __instance.ExplodeMineServerRpc();

            return true;
        }
    }

    [HarmonyPatch(typeof(ShipBuildModeManager), "Update")]
    public class ShipBuildModeManager_Update_Patch
    {
        public static void Postfix(ShipBuildModeManager __instance)
        {
            if (Settings.Instance.settingsData.b_PlaceAnywhere)
            {
                PlaceableShipObject placingObject = (PlaceableShipObject)PAUtils.GetValue(__instance, "placingObject", PAUtils.protectedFlags);
                if (placingObject)
                {
                    placingObject.AllowPlacementOnCounters = true;
                    placingObject.AllowPlacementOnWalls = true;
                    PAUtils.SetValue(__instance, "CanConfirmPosition", true, PAUtils.protectedFlags);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ShipBuildModeManager), "PlayerMeetsConditionsToBuild")]
    public class ShipBuildModeManager_PlayerMeetsConditionsToBuild_Patch
    {
        public static bool Prefix(ShipBuildModeManager __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_PlaceAnywhere)
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(GrabbableObject), "RequireCooldown")]
    public class GrabbableObject_RequireCooldown_Patch
    {
        public static bool Prefix(GrabbableObject __instance, ref bool __result)
        {
            if (Settings.Instance.settingsData.b_DisableInteractCooldowns)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }


    [HarmonyPatch(typeof(GrabbableObject), "DestroyObjectInHand")]
    public class GrabbableObject_DestroyObjectInHand_Patch
    {
        public static bool Prefix(GiftBoxItem __instance)
        {
            return !Settings.Instance.settingsData.b_InfiniteItems;
        }
    }

    [HarmonyPatch(typeof(InteractTrigger), "Interact")]
    public class InteractTrigger_Interact_Patch
    {
        public static bool Prefix(InteractTrigger __instance, Transform playerTransform)
        {

            __instance.interactCooldown = !Settings.Instance.settingsData.b_DisableInteractCooldowns;
            PlayerControllerB component = playerTransform.GetComponent<PlayerControllerB>();
            if (Settings.Instance.settingsData.switchingCam)
            {
                __instance.onInteract.Invoke(component);
                return false;
            } else
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ManualCameraRenderer), "MeetsCameraEnabledConditions")]
    public class AlwaysTruePatch
    {
        public static void Postfix(ref bool __result)
        {
                __result = true;
        }
    }

    [HarmonyPatch(typeof(PatcherTool), "LateUpdate")]
        public class PatcherTool_LateUpdate_Patch
        {
            public static void Postfix(PatcherTool __instance)
            {
                if (Settings.Instance.settingsData.b_InfiniteZapGun)
                {
                    __instance.gunOverheat = 0f;
                    __instance.bendMultiplier = 9999f;
                    __instance.pullStrength = 9999f;
                    PAUtils.SetValue(__instance, "timeSpentShocking", 0.01f, PAUtils.protectedFlags);
                }
            }
        }

        [HarmonyPatch(typeof(StunGrenadeItem), "ItemActivate")]
        public class StunGrenadeItem_ItemActivate_Patch
        {
            public static bool Prefix(StunGrenadeItem __instance)
            {
                if (Settings.Instance.settingsData.b_DisableInteractCooldowns)
                    __instance.inPullingPinAnimation = false;

                if (Settings.Instance.settingsData.b_InfiniteItems)
                {
                    __instance.itemUsedUp = false;

                    __instance.pinPulled = false;
                    __instance.hasExploded = false;
                    __instance.DestroyGrenade = false;
                    PAUtils.SetValue(__instance, "pullPinCoroutine", null, PAUtils.protectedFlags);
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(ShotgunItem), "ItemActivate")]
        public class ShotgunItem_ItemActivate_Patch
        {
            public static bool Prefix(ShotgunItem __instance)
            {
                if (Settings.Instance.settingsData.b_InfiniteShotgunAmmo)
                {
                    __instance.isReloading = false;
                    __instance.shellsLoaded++;
                }

                return true;
            }
    }

    [HarmonyPatch(typeof(StunGrenadeItem), "Update")]
    public static class NoFlash_Patch
    {
        public static void Postfix()
        {
            if (Settings.Instance.settingsData.b_NoFlash)
            {
                HUDManager.Instance.flashbangScreenFilter.weight = 0f;
                SoundManager.Instance.earsRingingTimer = 0f;
            }
        }
    }

        [HarmonyPatch(typeof(StartOfRound), "UpdatePlayerVoiceEffects")]
        public class StartOfRound_UpdatePlayerVoiceEffects_Patch
        {
            public static void Postfix(StartOfRound __instance)
            {
                if (Settings.Instance.settingsData.b_HearEveryone
                    && !StartOfRound.Instance.shipIsLeaving /* Without this you'll be stuck at "Wait for ship to land" - cba to find out way this happens */)
                {
                    for (int i = 0; i < __instance.allPlayerScripts.Length; i++)
                    {
                        PlayerControllerB playerControllerB = __instance.allPlayerScripts[i];
                        AudioSource currentVoiceChatAudioSource = playerControllerB.currentVoiceChatAudioSource;

                        currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>().enabled = false;
                        currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>().enabled = false;
                        currentVoiceChatAudioSource.panStereo = 0f;
                        SoundManager.Instance.playerVoicePitchTargets[(int)((IntPtr)playerControllerB.playerClientId)] = 1f;
                        SoundManager.Instance.SetPlayerPitch(1f, unchecked((int)playerControllerB.playerClientId));

                        currentVoiceChatAudioSource.spatialBlend = 0f;
                        playerControllerB.currentVoiceChatIngameSettings.set2D = true;
                        playerControllerB.voicePlayerState.Volume = 1f;
                    }
                }
            }
        }


        [HarmonyPatch(typeof(HUDManager), "HoldInteractionFill")]
        public class HUDManager_HoldInteractionFill_Patch
        {
            public static bool Prefix(HUDManager __instance, ref bool __result)
            {
                if (Settings.Instance.settingsData.b_InstantInteractions && !Settings.Instance.settingsData.holdingMouse)
                {
                    __result = true;
                    Settings.Instance.settingsData.holdingMouse = true;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SteamLobbyManager), "RefreshServerListButton")] // Removes the refresh cooldown
        public class SteamLobbyManager_RefreshServerListButton_Patch
        {
            public static bool Prefix(SteamLobbyManager __instance)
            {
                PAUtils.SetValue(__instance, "refreshServerListTimer", 1f, PAUtils.protectedFlags);
                return true;
            }
        }

        [HarmonyPatch(typeof(SteamLobbyManager), "loadLobbyListAndFilter")] // Forces lobbies with blacklisted names to appear
        public class SteamLobbyManager_loadLobbyListAndFilter_Patch
        {
            public static bool Prefix(SteamLobbyManager __instance)
            {
                __instance.censorOffensiveLobbyNames = false;
                return true;
            }
        }

        /* Graphical */

        [HarmonyPatch(typeof(Fog), "IsFogEnabled")]
        public class Fog_IsFogEnabled_Patch
        {
            public static bool Prefix(Fog __instance, ref bool __result)
            {
                if (Settings.Instance.settingsData.b_DisableFog)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Fog), "IsVolumetricFogEnabled")]
        public class Fog_IsVolumetricFogEnabled_Patch
        {
            public static bool Prefix(Fog __instance, ref bool __result)
            {
                if (Settings.Instance.settingsData.b_DisableFog)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Fog), "IsPBRFogEnabled")]
        public class Fog_IsPBRFogEnabled_Patch
        {
            public static bool Prefix(Fog __instance, ref bool __result)
            {
                if (Settings.Instance.settingsData.b_DisableFog)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Bloom), "IsActive")]
        public class Bloom_IsActive_Patch
        {
            public static bool Prefix(Bloom __instance, ref bool __result)
            {
                if (Settings.Instance.settingsData.b_DisableBloom)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(DepthOfField), "IsActive")]
        public class DepthOfField_IsActive_Patch
        {
            public static bool Prefix(DepthOfField __instance, ref bool __result)
            {
                if (Settings.Instance.settingsData.b_DisableDepthOfField)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Vignette), "IsActive")]
        public class Vignette_IsActive_Patch
        {
            public static bool Prefix(Vignette __instance, ref bool __result)
            {
                if (Settings.Instance.settingsData.b_DisableVignette)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(FilmGrain), "IsActive")]
        public class FilmGrain_IsActive_Patch
        {
            public static bool Prefix(FilmGrain __instance, ref bool __result)
            {
                if (Settings.Instance.settingsData.b_DisableFilmGrain)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }


        [HarmonyPatch(typeof(Exposure), "IsActive")]
        public class Exposure_IsActive_Patch
        {
            public static bool Prefix(Exposure __instance, ref bool __result)
            {
                if (Settings.Instance.settingsData.b_DisableFilmGrain)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch]
        internal class AdjustSmoothLookingPatcher
        {
            [HarmonyPatch(typeof(PlayerControllerB), "CalculateSmoothLookingInput")]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = new List<CodeInstruction>(instructions);
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].opcode == OpCodes.Ldc_R4 && (float)list[i].operand == 60f)
                    {
                        list[i].operand = 89f;
                        break;
                    }
                }
                return list.AsEnumerable();
            }
        }


    [HarmonyPatch]
        internal class AdjustNormalLookingPatcher
        {
            [HarmonyPatch(typeof(PlayerControllerB), "CalculateNormalLookingInput")]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = new List<CodeInstruction>(instructions);
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].opcode == OpCodes.Ldc_R4 && (float)list[i].operand == 60f)
                    {
                        list[i].operand = 89f;
                        break;
                    }
                }
                return list.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(Terminal))]
        internal class CostFixForSprayPaint
        {
            [HarmonyPatch("Update")]
            [HarmonyPrefix]
            private static void CostPatch(ref Item[] ___buyableItemsList)
            {
                if (Settings.Instance.settingsData.b_FreeShit)
                {
                    for (int i = 0; i < ___buyableItemsList.Length; i++)
                    {
                        ___buyableItemsList[i].creditsWorth = 0;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        public class UnlockablePriceChange
        {
            [HarmonyPostfix]
            private static void ChangeUnlockablePrice(PlayerControllerB __instance)
            {
                if (Settings.Instance.settingsData.b_FreeShit)
                {
                    UnlockablesList __unlockablesList = StartOfRound.Instance.unlockablesList;
                    if (__unlockablesList != null)
                    {
                        foreach (var unlockable in __unlockablesList.unlockables)
                        {
                            __instance.StartCoroutine(WaitUntilShopSelectionNodeInitialized(unlockable));
                        }
                    }
                    else
                    {
                        Debug.LogError("unlockables is null");
                    }
                }
            }

            private static IEnumerator WaitUntilShopSelectionNodeInitialized(UnlockableItem unlockable)
            {
                while (unlockable.shopSelectionNode == null)
                {
                    yield return null; // Wait for next frame
                }

                // At this point, shopSelectionNode has been initialized
                unlockable.shopSelectionNode.itemCost = 0;
                Debug.LogError("shopSelectionNode for unlockable: " + unlockable.unlockableName + " has been initialized.");
            }
        }




        [HarmonyPatch]
        internal class GodMode : MonoBehaviour
        {

            public void Update()
            {
                PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(PlayerControllerB), "DamagePlayer")]
            public static bool PrefixDamagePlayer(int damageNumber, bool hasDamageSFX = true, bool callRPC = true, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown, int deathAnimation = 0, bool fallDamage = false, Vector3 force = default(Vector3))
            {
                return !Settings.Instance.settingsData.b_GodMode;
            }

            // Token: 0x060000F4 RID: 244 RVA: 0x0000A2C2 File Offset: 0x000084C2
            [HarmonyPrefix]
            [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
            public static bool PrefixKillPlayer(Vector3 bodyVelocity, bool spawnBody = true, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown, int deathAnimation = 0)
            {
                return !Settings.Instance.settingsData.b_GodMode;
            }

            // Token: 0x060000F5 RID: 245 RVA: 0x0000A2D0 File Offset: 0x000084D0
            [HarmonyPrefix]
            [HarmonyPatch(typeof(FlowermanAI), "KillPlayerAnimationServerRpc")]
            public static bool PrefixFlowermanKill(int playerObjectId)
            {
                return !Settings.Instance.settingsData.b_GodMode;
            }

            // Token: 0x060000F6 RID: 246 RVA: 0x0000A300 File Offset: 0x00008500
            [HarmonyPrefix]
            [HarmonyPatch(typeof(ForestGiantAI), "GrabPlayerServerRpc")]
            public static bool PrefixGiantKill(int playerId)
            {
                return !Settings.Instance.settingsData.b_GodMode;
            }

            // Token: 0x060000F7 RID: 247 RVA: 0x0000A330 File Offset: 0x00008530
            [HarmonyPrefix]
            [HarmonyPatch(typeof(JesterAI), "KillPlayerServerRpc")]
            public static bool PrefixJesterKill(int playerId)
            {
                return !Settings.Instance.settingsData.b_GodMode;
            }

            // Token: 0x060000F8 RID: 248 RVA: 0x0000A360 File Offset: 0x00008560
            [HarmonyPrefix]
            [HarmonyPatch(typeof(MaskedPlayerEnemy), "KillPlayerAnimationServerRpc")]
            public static bool PrefixMaskedPlayerKill(int playerObjectId)
            {
                return !Settings.Instance.settingsData.b_GodMode;
            }

            // Token: 0x060000F9 RID: 249 RVA: 0x0000A390 File Offset: 0x00008590
            [HarmonyPrefix]
            [HarmonyPatch(typeof(MouthDogAI), "OnCollideWithPlayer")]
            public static bool PrefixDogKill(MouthDogAI __instance, Collider other)
            {
                return !Settings.Instance.settingsData.b_GodMode;
            }

            // Token: 0x060000FA RID: 250 RVA: 0x0000A3CC File Offset: 0x000085CC
            [HarmonyPrefix]
            [HarmonyPatch(typeof(CentipedeAI), "OnCollideWithPlayer")]
            public static bool PrefixCentipedeCling(CentipedeAI __instance, Collider other)
            {
                return !Settings.Instance.settingsData.b_GodMode;
            }
        }
    }