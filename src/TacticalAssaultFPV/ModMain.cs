using System;
using System.Collections;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppInterop.Runtime.Injection;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppHurricaneVR.Framework.Core.Grabbers;
using Il2CppHurricaneVR.Framework.Core.HandPoser;
using Il2CppHurricaneVR.Framework.Core.Sockets;
using Il2CppHurricaneVR.Framework.Core.Player;
using TacticalAssaultFPV.Drone;
using TacticalAssaultFPV.Controller;
using TacticalAssaultFPV.Config;
using TacticalAssaultFPV.Loadout;

[assembly: MelonInfo(typeof(TacticalAssaultFPV.ModMain), "TacticalAssaultFPV", "1.0.0", "Dmitry")]
[assembly: MelonGame("Twisted Barrel Games", "Tactical Assault VR")]

namespace TacticalAssaultFPV;

public class ModMain : MelonMod
{
    private static ModMain _instance;
    public static ModMain Instance => _instance;

    public override void OnInitializeMelon()
    {
        _instance = this;

        ClassInjector.RegisterTypeInIl2Cpp<DronePhysics>();
        ClassInjector.RegisterTypeInIl2Cpp<DroneExplosive>();
        ClassInjector.RegisterTypeInIl2Cpp<DroneCameraRig>();
        ClassInjector.RegisterTypeInIl2Cpp<RCTablet>();
        ClassInjector.RegisterTypeInIl2Cpp<ArmoryEquipButtonTrigger>();
        ClassInjector.RegisterTypeInIl2Cpp<DroppedPayload>();

        MelonLogger.Msg("TacticalAssaultFPV Mod initialized! Custom IL2CPP types registered.");
    }

    private static bool _inputWarningLogged = false;

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        try
        {
            MelonLogger.Msg($"[ModMain] OnSceneWasLoaded: buildIndex={buildIndex}, sceneName='{sceneName}'");

            // 1. Reset locomotion lock and active flight state
            RCTablet.ResetPlayerFrozen();

            // 2. Clear scene-bound GameObjects and drone lists in loadout integration
            FPVLoadoutIntegration.ClearSceneState();

            // 3. Reset cached competition timer prefab in DroneFactory
            DroneFactory.CompetitionTimerPrefab = null;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[ModMain] OnSceneWasLoaded error: {ex}");
        }
    }

    public override void OnUpdate()
    {
        if (Application.isBatchMode) return;

        try
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F8))
            {
                TriggerEmergencySpawn();
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
            {
                TriggerDiagnosticsSelfTest();
            }
        }
        catch (Exception ex)
        {
            if (!_inputWarningLogged)
            {
                _inputWarningLogged = true;
                MelonLogger.Warning($"[ModMain] Keyboard input polling unavailable: {ex.Message}");
            }
        }
    }

    private static void TriggerEmergencySpawn()
    {
        try
        {
            MelonLogger.Msg("[ModMain] F8 pressed: Spawning fresh FPV Drone & RC Tablet kit...");

            // 1. Resolve player head / camera orientation
            Transform head = null;
            if (Camera.main != null)
            {
                head = Camera.main.transform;
            }
            if (head == null)
            {
                var hvr = UnityEngine.Object.FindObjectOfType<HVRPlayerController>();
                if (hvr != null && hvr.Camera != null)
                {
                    head = hvr.Camera;
                }
            }
            if (head == null)
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
                if (player != null)
                {
                    head = player.transform;
                }
            }

            if (head == null)
            {
                MelonLogger.Warning("[ModMain] F8 Spawn aborted: No active player or camera found in scene.");
                return;
            }

            // 2. Compute spawn position ~0.7m in front of player's face
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = head.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 centerPos = head.position + forward * 0.7f;
            Vector3 dronePos = centerPos - right * 0.22f;
            Vector3 tabletPos = centerPos + right * 0.22f;
            Quaternion orientation = Quaternion.LookRotation(forward, Vector3.up);

            // 3. Reset any lingering locomotion freeze
            RCTablet.ResetPlayerFrozen();

            // 4. Instantiate fresh Drone and RC Tablet
            GameObject drone = DroneFactory.CreateDrone(dronePos, orientation);
            GameObject tablet = DroneFactory.CreateRCTablet(tabletPos, orientation);

            // 5. Zero out initial rigidbody velocities
            if (drone != null)
            {
                var rb = drone.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            if (tablet != null)
            {
                var rb = tablet.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // 6. Pair fresh drone with fresh tablet
            if (drone != null && tablet != null)
            {
                var phys = drone.GetComponent<DronePhysics>();
                var cam = drone.GetComponent<DroneCameraRig>();
                var tab = tablet.GetComponent<RCTablet>();
                if (tab != null)
                {
                    tab.RegisterDrone(drone);
                    if (phys != null && cam != null)
                    {
                        tab.PairWithDrone(phys, cam);
                    }
                }

                FPVLoadoutIntegration.CurrentLoadoutMode = FPVLoadoutIntegration.FPVLoadoutMode.ControllerKit;
                MelonLogger.Msg($"[ModMain] F8 Emergency Kit successfully spawned at {centerPos}!");
            }
            else
            {
                MelonLogger.Error($"[ModMain] F8 Emergency Kit failed to instantiate: drone={drone != null}, tablet={tablet != null}");
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[ModMain] F8 Emergency Spawn error: {ex}");
        }
    }

    private static void TriggerDiagnosticsSelfTest()
    {
        try
        {
            MelonLogger.Msg("[ModMain] F9 pressed: Triggering automated subsystem self-test...");
            Diagnostics.RunSelfTest();
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[ModMain] F9 Diagnostics trigger error: {ex}");
        }
    }

    /// <summary>
    /// Checks if the current scene is a briefing room, lobby, or armory where terminal station should spawn.
    /// Prevents spawning briefing terminals inside active combat mission scenes.
    /// </summary>
    public static bool IsLobbyOrArmoryScene()
    {
        try
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.name))
            {
                string name = scene.name;
                if (name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Armory", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (SceneLoaderManager.Instance != null && SceneLoaderManager.Instance.isMainMenu)
            {
                return true;
            }

            if (UnityEngine.Object.FindObjectOfType<EquipButtonManager>() != null)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[ModMain] Error evaluating scene context: {ex.Message}");
        }

        return false;
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Start))]
    public static class PlayerHealth_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerHealth __instance)
        {
            try
            {
                if (__instance == null || __instance.transform == null) return;

                // Restrict to local human player: ignore target dummies and remote players
                if (__instance.isDummyHealth) return;
                if (__instance.photonView != null && !__instance.photonView.IsMine) return;

                // Ensure locomotion freeze is reset on player spawn / respawn (Feature #10)
                RCTablet.ResetPlayerFrozen();

                // Setup FPV items in grenade selection menus and spawn briefing terminal ONLY in lobby/armory
                if (IsLobbyOrArmoryScene())
                {
                    FPVLoadoutIntegration.SetupGrenadeMenus();
                    Diagnostics.DumpMenusOnce();
                    FPVLoadoutIntegration.SetupArmoryTerminal(__instance.transform.position, __instance.transform.rotation);
                }

                // If drone was previously selected, equip to mission scene sockets
                if (FPVLoadoutIntegration.IsFPVDroneSelected)
                {
                    MelonCoroutines.Start(DelayedSceneEquip(0.8f));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"PlayerHealth.Start patch error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(HVRPlayerController), nameof(HVRPlayerController.HandleMovement))]
    public static class HVRPlayerController_HandleMovement_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(HVRPlayerController __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            if (RCTablet.IsPlayerFrozen)
            {
                __instance.move = Vector3.zero;
                __instance.xzVelocity = Vector3.zero;
                if (__instance.RigidBody != null)
                {
                    __instance.RigidBody.linearVelocity = new Vector3(0f, __instance.RigidBody.linearVelocity.y, 0f);
                }
                return false; // Skip original locomotion movement completely while controlling drone!
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(HVRPlayerController), nameof(HVRPlayerController.HandleRotation))]
    public static class HVRPlayerController_HandleRotation_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(HVRPlayerController __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            if (RCTablet.IsPlayerFrozen)
            {
                return false; // Skip thumbstick rotation completely while controlling drone!
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(EquipButtonManager), nameof(EquipButtonManager.Equip))]
    public static class EquipButtonManager_Equip_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(EquipButtonManager __instance)
        {
            try
            {
                if (__instance == null || __instance.transform == null) return;

                MelonLogger.Msg("[ModMain] EquipButtonManager.Equip called!");
                FPVLoadoutIntegration.OnEquipButtonPressed(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"EquipButtonManager.Equip patch error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(NetworkPlayerSpawner), nameof(NetworkPlayerSpawner.SpawnPrefab))]
    public static class NetworkPlayerSpawner_SpawnPrefab_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(NetworkPlayerSpawner __instance)
        {
            try
            {
                if (__instance == null) return;

                // Guard against remote player avatar spawns
                if (__instance.photonView != null && !__instance.photonView.IsMine) return;
                if (__instance.playerHealth != null)
                {
                    if (__instance.playerHealth.isDummyHealth) return;
                    if (__instance.playerHealth.photonView != null && !__instance.playerHealth.photonView.IsMine) return;
                }

                MelonLogger.Msg("[ModMain] NetworkPlayerSpawner.SpawnPrefab called for local player!");
                if (FPVLoadoutIntegration.IsFPVDroneSelected)
                {
                    MelonCoroutines.Start(DelayedSceneEquip(1.0f));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"NetworkPlayerSpawner.SpawnPrefab patch error: {ex}");
            }
        }
    }

    // --- Menu Gallery Button Patch ---

    [HarmonyPatch(typeof(MenuController), nameof(MenuController.OnClick))]
    public static class MenuController_OnClick_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(MenuController __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            if (__instance.name.StartsWith("FPV_Controller_Button"))
            {
                if (__instance.OnClicksfx != null) __instance.OnClicksfx.Play();
                if (__instance.menusToDisable != null)
                {
                    foreach (var go in __instance.menusToDisable) if (go != null) go.SetActive(false);
                }
                if (__instance.menusToEnable != null)
                {
                    foreach (var go in __instance.menusToEnable) if (go != null) go.SetActive(true);
                }

                int slot = __instance.name.EndsWith("Slot1") ? 1 : 2;
                FPVLoadoutIntegration.SelectSlotMode(slot, FPVLoadoutIntegration.FPVUtilitySlotMode.ControllerKit);
                return false;
            }
            else if (__instance.name.StartsWith("FPV_Drone_Button"))
            {
                if (__instance.OnClicksfx != null) __instance.OnClicksfx.Play();
                if (__instance.menusToDisable != null)
                {
                    foreach (var go in __instance.menusToDisable) if (go != null) go.SetActive(false);
                }
                if (__instance.menusToEnable != null)
                {
                    foreach (var go in __instance.menusToEnable) if (go != null) go.SetActive(true);
                }

                int slot = __instance.name.EndsWith("Slot1") ? 1 : 2;
                FPVLoadoutIntegration.SelectSlotMode(slot, FPVLoadoutIntegration.FPVUtilitySlotMode.DroneOnly);
                return false;
            }

            return true;
        }
    }

    // --- Weapon Attachments Spawner Hook (Bypasses impact grenades!) ---

    [HarmonyPatch(typeof(WeaponAttachmentsSpawner), nameof(WeaponAttachmentsSpawner.Selection))]
    public static class WeaponAttachmentsSpawner_Selection_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponAttachmentsSpawner __instance, Transform _transform)
        {
            if (__instance == null || __instance.transform == null) return true;

            string sName = __instance.name.ToLower();
            bool isSlot1 = __instance.utility1 || sName.Contains("utility1");
            bool isSlot2 = __instance.utility2 || sName.Contains("utility2");

            if (isSlot1 && FPVLoadoutIntegration.IsSlot1FPV) return false;
            if (isSlot2 && FPVLoadoutIntegration.IsSlot2FPV) return false;

            return true;
        }
    }

    // --- Grenade Menu (Utility 1) Patches ---

    [HarmonyPatch(typeof(UtilityOneSelect), nameof(UtilityOneSelect.OnEnable))]
    public static class UtilityOneSelect_OnEnable_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UtilityOneSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return;
            FPVLoadoutIntegration.SetupGrenadeMenus();
        }
    }

    [HarmonyPatch(typeof(UtilityOneSelect), nameof(UtilityOneSelect.NextWeapon))]
    public static class UtilityOneSelect_NextWeapon_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(UtilityOneSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            int total = __instance.transform.childCount;
            if (total > 15)
            {
                __instance.selectedUtilityOne = (__instance.selectedUtilityOne + 1) % total;
                __instance.SelectWeapon();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UtilityOneSelect), nameof(UtilityOneSelect.NextWeaponButton))]
    public static class UtilityOneSelect_Next_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(UtilityOneSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            int total = __instance.transform.childCount;
            if (total > 15)
            {
                __instance.selectedUtilityOne = (__instance.selectedUtilityOne + 1) % total;
                __instance.SelectWeapon();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UtilityOneSelect), nameof(UtilityOneSelect.PreviousWeapon))]
    public static class UtilityOneSelect_PreviousWeapon_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(UtilityOneSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            int total = __instance.transform.childCount;
            if (total > 15)
            {
                __instance.selectedUtilityOne = (__instance.selectedUtilityOne - 1 + total) % total;
                __instance.SelectWeapon();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UtilityOneSelect), nameof(UtilityOneSelect.PreviousWeaponButton))]
    public static class UtilityOneSelect_Prev_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(UtilityOneSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            int total = __instance.transform.childCount;
            if (total > 15)
            {
                __instance.selectedUtilityOne = (__instance.selectedUtilityOne - 1 + total) % total;
                __instance.SelectWeapon();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UtilityOneSelect), nameof(UtilityOneSelect.SelectWeapon))]
    public static class UtilityOneSelect_SelectWeapon_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UtilityOneSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return;
            FPVLoadoutIntegration.OnUtilityOneSelectWeapon(__instance);
        }
    }

    // --- Grenade Menu (Utility 2) Patches ---

    [HarmonyPatch(typeof(UtilityTwoSelect), nameof(UtilityTwoSelect.OnEnable))]
    public static class UtilityTwoSelect_OnEnable_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UtilityTwoSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return;
            FPVLoadoutIntegration.SetupGrenadeMenus();
        }
    }

    [HarmonyPatch(typeof(UtilityTwoSelect), nameof(UtilityTwoSelect.NextWeapon))]
    public static class UtilityTwoSelect_NextWeapon_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(UtilityTwoSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            int total = __instance.transform.childCount;
            if (total > 15)
            {
                __instance.selectedUtilityTwo = (__instance.selectedUtilityTwo + 1) % total;
                __instance.SelectWeapon();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UtilityTwoSelect), nameof(UtilityTwoSelect.NextWeaponButton))]
    public static class UtilityTwoSelect_Next_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(UtilityTwoSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            int total = __instance.transform.childCount;
            if (total > 15)
            {
                __instance.selectedUtilityTwo = (__instance.selectedUtilityTwo + 1) % total;
                __instance.SelectWeapon();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UtilityTwoSelect), nameof(UtilityTwoSelect.PreviousWeapon))]
    public static class UtilityTwoSelect_PreviousWeapon_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(UtilityTwoSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            int total = __instance.transform.childCount;
            if (total > 15)
            {
                __instance.selectedUtilityTwo = (__instance.selectedUtilityTwo - 1 + total) % total;
                __instance.SelectWeapon();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UtilityTwoSelect), nameof(UtilityTwoSelect.PreviousWeaponButton))]
    public static class UtilityTwoSelect_Prev_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(UtilityTwoSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return true;

            int total = __instance.transform.childCount;
            if (total > 15)
            {
                __instance.selectedUtilityTwo = (__instance.selectedUtilityTwo - 1 + total) % total;
                __instance.SelectWeapon();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UtilityTwoSelect), nameof(UtilityTwoSelect.SelectWeapon))]
    public static class UtilityTwoSelect_SelectWeapon_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UtilityTwoSelect __instance)
        {
            if (__instance == null || __instance.transform == null) return;
            FPVLoadoutIntegration.OnUtilityTwoSelectWeapon(__instance);
        }
    }

    private static IEnumerator DelayedSceneEquip(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        MelonLogger.Msg("[ModMain] Executing DelayedSceneEquip into vest sockets...");
        FPVLoadoutIntegration.EquipToSceneSockets();
    }
}
