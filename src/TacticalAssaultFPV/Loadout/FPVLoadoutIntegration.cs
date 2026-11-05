using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppHurricaneVR.Framework.Core.Grabbers;
using Il2CppHurricaneVR.Framework.Core.Sockets;
using Il2CppHurricaneVR.Framework.Core.Player;
using TacticalAssaultFPV.Drone;
using TacticalAssaultFPV.Controller;

namespace TacticalAssaultFPV.Loadout;

public static class FPVLoadoutIntegration
{
    public enum FPVLoadoutMode
    {
        None,
        ControllerKit, // Controller/Tablet in utility 2, Drone in utility 1
        DualDrone      // Drone in utility 1, Drone in utility 2
    }

    public static FPVLoadoutMode CurrentLoadoutMode { get; set; } = FPVLoadoutMode.None;
    public static bool IsFPVDroneSelected => CurrentLoadoutMode != FPVLoadoutMode.None;

    private static GameObject _armoryTerminal;
    private static GameObject _stationDrone;
    private static GameObject _stationTablet;
    private static Vector3 _terminalPos;
    private static Quaternion _terminalRot;

    private static GameObject _activeEquippedDrone;
    private static GameObject _activeEquippedTablet;
    public static GameObject ActiveEquippedTablet
    {
        get => _activeEquippedTablet;
        set => _activeEquippedTablet = value;
    }

    /// <summary>
    /// Injects FPV Strike Drone entry directly into the in-game 2D UI Grenade selection menus (Utility 1 & Utility 2)
    /// </summary>
    public static void SetupGrenadeMenus()
    {
        try
        {
            // 1. Setup Utility 1 (Grenade Slot 1 UI Menu)
            var u1All = Resources.FindObjectsOfTypeAll<UtilityOneSelect>();
            foreach (var u in u1All)
            {
                if (u == null || u.transform == null) continue;
                InjectEntryIntoMenu(u.transform, "FPV_Drone_Entry_1");
            }

            // 2. Setup Utility 2 (Grenade Slot 2 UI Menu)
            var u2All = Resources.FindObjectsOfTypeAll<UtilityTwoSelect>();
            foreach (var u in u2All)
            {
                if (u == null || u.transform == null) continue;
                InjectEntryIntoMenu(u.transform, "FPV_Drone_Entry_2");
            }

            // 3. Expand EquipButtonManager spawner attachments arrays so index 15 is valid across all utility spawners
            EquipButtonManager equipMgr = UnityEngine.Object.FindObjectOfType<EquipButtonManager>();
            if (equipMgr != null)
            {
                if (equipMgr.utility1Spawner != null && equipMgr.utility1Spawner.Attachments != null)
                {
                    foreach (var att in equipMgr.utility1Spawner.Attachments)
                    {
                        if (att != null && att.name.StartsWith("CompetitionTimer"))
                        {
                            DroneFactory.CompetitionTimerPrefab = att;
                            MelonLoader.MelonLogger.Msg("[Loadout] Cached CompetitionTimer prefab from utility1Spawner!");
                            break;
                        }
                    }
                }

                ExpandSpawnerAttachments(equipMgr.utility1Spawner);
                ExpandSpawnerAttachments(equipMgr.utility11Spawner);
                ExpandSpawnerAttachments(equipMgr.utility2Spawner);
                ExpandSpawnerAttachments(equipMgr.utility22Spawner);
            }

            foreach (var sp in Resources.FindObjectsOfTypeAll<WeaponAttachmentsSpawner>())
            {
                if (sp != null && (sp.utility1 || sp.utility2 || sp.name.ToLower().Contains("utility")))
                {
                    ExpandSpawnerAttachments(sp);
                }
            }

            // 4. Inject FPV Drone button directly into the grenade gallery menus
            InjectGalleryButtons();
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"Error in SetupGrenadeMenus: {ex.Message}");
        }
    }

    public static void InjectGalleryButtons()
    {
        try
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                if (go.name == "Utilities_slot1_Gallery")
                {
                    InjectButtonIntoGallery(go.transform, 1);
                }
                else if (go.name == "Utilities_slot2_Gallery")
                {
                    InjectButtonIntoGallery(go.transform, 2);
                }
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"Error in InjectGalleryButtons: {ex.Message}");
        }
    }

    private static void InjectButtonIntoGallery(Transform galleryTr, int slotNum)
    {
        if (galleryTr == null) return;

        Transform hg = galleryTr.Find("Gallery/HorizontalGroup (2)") ?? galleryTr.Find("Gallery/HorizontalGroup (1)");
        if (hg == null || hg.childCount == 0) return;

        Transform template = hg.GetChild(0);

        // 1. Inject "FPV CONTROLLER" (Kit: Controller + Drone)
        string controllerBtnName = (slotNum == 1) ? "FPV_Controller_Button_Slot1" : "FPV_Controller_Button_Slot2";
        if (hg.Find(controllerBtnName) == null)
        {
            GameObject btnCtrl = GameObject.Instantiate(template.gameObject, hg, false);
            btnCtrl.name = controllerBtnName;

            Text txt = btnCtrl.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                txt.text = "FPV CONTROLLER";
                txt.color = new Color(0.2f, 0.85f, 1.0f); // Cyan
            }

            Transform imgTr = btnCtrl.transform.Find("mask/image");
            if (imgTr != null)
            {
                Image img = imgTr.GetComponent<Image>();
                if (img != null) img.color = new Color(0.2f, 0.85f, 1.0f);
            }

            var mc = btnCtrl.GetComponent<MenuController>();
            if (mc != null && mc.buttonPressEvents != null)
            {
                mc.buttonPressEvents.RemoveAllListeners();
            }

            btnCtrl.SetActive(true);
            MelonLoader.MelonLogger.Msg($"[Loadout] Injected '{controllerBtnName}' into '{galleryTr.name}'!");
        }

        // 2. Inject "FPV DRONE" (Dual Drones across both slots)
        string droneBtnName = (slotNum == 1) ? "FPV_Drone_Button_Slot1" : "FPV_Drone_Button_Slot2";
        if (hg.Find(droneBtnName) == null)
        {
            GameObject btnDrone = GameObject.Instantiate(template.gameObject, hg, false);
            btnDrone.name = droneBtnName;

            Text txt = btnDrone.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                txt.text = "FPV DRONE";
                txt.color = new Color(0.2f, 1.0f, 0.35f); // Green
            }

            Transform imgTr = btnDrone.transform.Find("mask/image");
            if (imgTr != null)
            {
                Image img = imgTr.GetComponent<Image>();
                if (img != null) img.color = new Color(0.2f, 1.0f, 0.35f);
            }

            var mc = btnDrone.GetComponent<MenuController>();
            if (mc != null && mc.buttonPressEvents != null)
            {
                mc.buttonPressEvents.RemoveAllListeners();
            }

            btnDrone.SetActive(true);
            MelonLoader.MelonLogger.Msg($"[Loadout] Injected '{droneBtnName}' into '{galleryTr.name}'!");
        }
    }

    public static void SelectLoadoutMode(FPVLoadoutMode mode)
    {
        try
        {
            CurrentLoadoutMode = mode;
            MelonLoader.MelonLogger.Msg($"[Loadout] Selected FPV Loadout Mode: {mode}");

            var u1 = UnityEngine.Object.FindObjectOfType<UtilityOneSelect>();
            if (u1 == null)
            {
                var all = Resources.FindObjectsOfTypeAll<UtilityOneSelect>();
                if (all.Length > 0) u1 = all[0];
            }

            var u2 = UnityEngine.Object.FindObjectOfType<UtilityTwoSelect>();
            if (u2 == null)
            {
                var all = Resources.FindObjectsOfTypeAll<UtilityTwoSelect>();
                if (all.Length > 0) u2 = all[0];
            }

            if (mode == FPVLoadoutMode.ControllerKit)
            {
                if (u1 != null && u1.transform.childCount > 0)
                {
                    int idx = u1.transform.childCount - 1;
                    u1.selectedUtilityOne = idx;
                    SetPreviewText(u1.transform.GetChild(idx), "FPV DRONE", new Color(0.2f, 1.0f, 0.35f));
                    u1.SelectWeapon();
                }
                if (u2 != null && u2.transform.childCount > 0)
                {
                    int idx = u2.transform.childCount - 1;
                    u2.selectedUtilityTwo = idx;
                    SetPreviewText(u2.transform.GetChild(idx), "FPV CONTROLLER", new Color(0.2f, 0.85f, 1.0f));
                    u2.SelectWeapon();
                }
            }
            else if (mode == FPVLoadoutMode.DualDrone)
            {
                if (u1 != null && u1.transform.childCount > 0)
                {
                    int idx = u1.transform.childCount - 1;
                    u1.selectedUtilityOne = idx;
                    SetPreviewText(u1.transform.GetChild(idx), "FPV DRONE", new Color(0.2f, 1.0f, 0.35f));
                    u1.SelectWeapon();
                }
                if (u2 != null && u2.transform.childCount > 0)
                {
                    int idx = u2.transform.childCount - 1;
                    u2.selectedUtilityTwo = idx;
                    SetPreviewText(u2.transform.GetChild(idx), "FPV DRONE", new Color(0.2f, 1.0f, 0.35f));
                    u2.SelectWeapon();
                }
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"Error selecting FPV loadout mode: {ex}");
        }
    }

    private static void SetPreviewText(Transform entry, string text, Color color)
    {
        if (entry == null) return;
        Text txt = entry.GetComponentInChildren<Text>(true);
        if (txt != null)
        {
            txt.text = text;
            txt.color = color;
        }
        RawImage img = entry.GetComponentInChildren<RawImage>(true);
        if (img != null)
        {
            img.color = color;
        }
    }

    private static readonly System.Collections.Generic.List<GameObject> _activeDrones = new System.Collections.Generic.List<GameObject>();
    public static System.Collections.Generic.List<GameObject> ActiveDrones => _activeDrones;

    private static readonly System.Collections.Generic.Dictionary<WeaponAttachmentsSpawner, float> _spawnerCooldowns =
        new System.Collections.Generic.Dictionary<WeaponAttachmentsSpawner, float>();
    private static float _lastEquipButtonTime = -999f;

    /// <summary>
    /// Directly equips FPV items into weapon spawner sockets:
    /// In ControllerKit mode: 3 Drones (Utility1, Utility1_1, Utility2_2) + 1 Tablet (Utility2)
    /// </summary>
    public static void SpawnForUtilitySpawner(WeaponAttachmentsSpawner spawner, int slotNum)
    {
        try
        {
            if (CurrentLoadoutMode == FPVLoadoutMode.None) return;
            if (spawner == null) return;

            // Debounce spawner calls within 0.2s to prevent duplicate execution within the same frame
            if (_spawnerCooldowns.TryGetValue(spawner, out float lastTime) && Time.time - lastTime < 0.2f)
            {
                return;
            }
            _spawnerCooldowns[spawner] = Time.time;

            HVRSocket socket = spawner.HVRSocket;
            if (socket == null) return;

            PrepareSocketForGrabbable(socket);

            if (socket.GrabbedTarget != null)
            {
                // If this socket already holds an active FPV Drone or Tablet, keep it!
                if (socket.GrabbedTarget.name.StartsWith("FPV_"))
                {
                    if (socket.GrabbedTarget.name.Contains("Drone"))
                    {
                        RegisterActiveDrone(socket.GrabbedTarget.gameObject);
                    }
                    return;
                }
                UnityEngine.Object.Destroy(socket.GrabbedTarget.gameObject);
            }

            Vector3 pos = socket.transform.position;
            Quaternion rot = socket.transform.rotation;
            string sName = spawner.name.ToLower();

            if (CurrentLoadoutMode == FPVLoadoutMode.ControllerKit)
            {
                // In ControllerKit:
                // Utility2Spawner gets the RC Tablet.
                // Utility1Spawner, Utility1_1Spawner, and Utility2_2Spawner each get a Drone! (Total = 3 Drones + 1 Tablet)
                bool isTabletSlot = (sName == "utility2spawner" || (slotNum == 2 && !sName.Contains("_2") && !sName.Contains("22")));

                if (isTabletSlot)
                {
                    if (_activeEquippedTablet != null) UnityEngine.Object.Destroy(_activeEquippedTablet);
                    _activeEquippedTablet = DroneFactory.CreateRCTablet(pos, rot);
                    var grabbable = _activeEquippedTablet.GetComponent<HVRGrabbable>();
                    bool grabbed = socket.TryGrab(grabbable, true, true);
                    MelonLoader.MelonLogger.Msg($"[Loadout] Equipped RC Tablet into socket: {socket.gameObject.name} (Grabbed={grabbed})");
                }
                else
                {
                    GameObject drone = DroneFactory.CreateDrone(pos, rot);
                    var grabbable = drone.GetComponent<HVRGrabbable>();
                    bool grabbed = socket.TryGrab(grabbable, true, true);
                    if (!grabbed)
                    {
                        drone.transform.position = pos;
                        drone.transform.rotation = rot;
                    }
                    RegisterActiveDrone(drone);
                    MelonLoader.MelonLogger.Msg($"[Loadout] Equipped FPV Drone into socket: {socket.gameObject.name} (Grabbed={grabbed}, Total drones: {_activeDrones.Count})");
                }
            }
            else if (CurrentLoadoutMode == FPVLoadoutMode.DualDrone)
            {
                GameObject drone = DroneFactory.CreateDrone(pos, rot);
                var grabbable = drone.GetComponent<HVRGrabbable>();
                bool grabbed = socket.TryGrab(grabbable, true, true);
                if (!grabbed)
                {
                    drone.transform.position = pos;
                    drone.transform.rotation = rot;
                }
                RegisterActiveDrone(drone);
                MelonLoader.MelonLogger.Msg($"[Loadout] Equipped FPV Drone into socket: {socket.gameObject.name} (Grabbed={grabbed})");
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"Error in SpawnForUtilitySpawner ({spawner?.name}): {ex}");
        }
    }

    public static void RegisterActiveDrone(GameObject drone)
    {
        if (drone != null && !_activeDrones.Contains(drone))
        {
            _activeDrones.Add(drone);
        }
    }

    public static void SyncTabletDrones()
    {
        if (_activeEquippedTablet == null) return;
        var tab = _activeEquippedTablet.GetComponent<RCTablet>();
        if (tab == null) return;

        for (int i = _activeDrones.Count - 1; i >= 0; i--)
        {
            if (_activeDrones[i] == null)
            {
                _activeDrones.RemoveAt(i);
            }
        }

        // Only register drones that the pilot has explicitly powered on via trigger!
        var powered = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < _activeDrones.Count; i++)
        {
            var d = _activeDrones[i];
            if (d != null)
            {
                var phys = d.GetComponent<DronePhysics>();
                if (phys != null && phys.IsPoweredOn)
                {
                    powered.Add(d);
                }
            }
        }
        tab.RegisterDrones(powered);
    }

    private static void InjectEntryIntoMenu(Transform menuTransform, string entryName)
    {
        if (menuTransform == null) return;
        Transform existing = menuTransform.Find(entryName);
        if (existing != null) return;

        // Clone Child[4] (Impact_Grenade UI entry with RawImage and Text)
        int sourceIndex = Mathf.Min(4, menuTransform.childCount - 1);
        if (sourceIndex < 0) return;

        GameObject template = menuTransform.GetChild(sourceIndex).gameObject;
        GameObject newEntry = GameObject.Instantiate(template, menuTransform, false);
        newEntry.name = entryName;

        Text txt = newEntry.GetComponentInChildren<Text>(true);
        if (txt != null)
        {
            txt.text = "FPV STRIKE DRONE";
            txt.color = new Color(0.2f, 1.0f, 0.4f);
        }

        RawImage img = newEntry.GetComponentInChildren<RawImage>(true);
        if (img != null)
        {
            img.color = new Color(0.2f, 0.85f, 0.35f);
        }

        newEntry.SetActive(false);
        MelonLoader.MelonLogger.Msg($"[Loadout] Injected '{entryName}' into menu '{menuTransform.parent?.name}' at index {menuTransform.childCount - 1}!");
    }

    private static void ExpandSpawnerAttachments(WeaponAttachmentsSpawner spawner)
    {
        if (spawner == null || spawner.Attachments == null) return;
        // Only grenade/utility loadout spawners (which have 15 grenade entries) need expansion to 16
        if (spawner.Attachments.Length < 10 || spawner.Attachments.Length >= 16) return;

        int oldLen = spawner.Attachments.Length;
        var newArray = new Il2CppReferenceArray<GameObject>(oldLen + 1);
        for (int i = 0; i < oldLen; i++)
        {
            newArray[i] = spawner.Attachments[i];
        }
        // Fill 16th slot with base template from slot 4 (impact grenade) or last available
        newArray[oldLen] = spawner.Attachments[Mathf.Clamp(4, 0, oldLen - 1)];
        spawner.Attachments = newArray;
        MelonLoader.MelonLogger.Msg($"[Loadout] Expanded attachments on spawner '{spawner.name}' to {newArray.Length}");
    }

    public static void OnUtilityOneSelectWeapon(UtilityOneSelect u)
    {
        if (u == null) return;
        int total = u.transform.childCount;
        bool isDrone = (u.selectedUtilityOne == total - 1);

        for (int i = 0; i < total; i++)
        {
            u.transform.GetChild(i).gameObject.SetActive(i == u.selectedUtilityOne);
        }

        if (!isDrone && CurrentLoadoutMode != FPVLoadoutMode.None)
        {
            CurrentLoadoutMode = FPVLoadoutMode.None;
            RestoreAllSocketFilters();
            MelonLoader.MelonLogger.Msg("[Loadout] Player chose standard utility 1, FPV mode cleared.");
        }
    }

    public static void OnUtilityTwoSelectWeapon(UtilityTwoSelect u)
    {
        if (u == null) return;
        int total = u.transform.childCount;
        bool isDrone = (u.selectedUtilityTwo == total - 1);

        for (int i = 0; i < total; i++)
        {
            u.transform.GetChild(i).gameObject.SetActive(i == u.selectedUtilityTwo);
        }

        if (!isDrone && CurrentLoadoutMode != FPVLoadoutMode.None)
        {
            CurrentLoadoutMode = FPVLoadoutMode.None;
            RestoreAllSocketFilters();
            MelonLoader.MelonLogger.Msg("[Loadout] Player chose standard utility 2, FPV mode cleared.");
        }
    }

    /// <summary>
    /// Called when the player presses the armory's main EQUIP button
    /// </summary>
    public static void OnEquipButtonPressed(EquipButtonManager equipManager)
    {
        try
        {
            if (Time.time - _lastEquipButtonTime < 1.2f)
            {
                MelonLoader.MelonLogger.Msg($"[Loadout] Debounced duplicate EquipButtonPressed call (ignored, dt={Time.time - _lastEquipButtonTime:F3}s).");
                return;
            }
            _lastEquipButtonTime = Time.time;

            MelonLoader.MelonLogger.Msg($"[Loadout] EquipButtonManager.Equip called. CurrentLoadoutMode={CurrentLoadoutMode}");
            if (CurrentLoadoutMode != FPVLoadoutMode.None && equipManager != null)
            {
                _spawnerCooldowns.Clear();

                // Clean up stale or null drone references before re-populating (D3)
                for (int i = _activeDrones.Count - 1; i >= 0; i--)
                {
                    var d = _activeDrones[i];
                    if (d != null)
                    {
                        try { UnityEngine.Object.Destroy(d); } catch { }
                    }
                }
                _activeDrones.Clear();

                if (equipManager.utility1Spawner != null) SpawnForUtilitySpawner(equipManager.utility1Spawner, 1);
                if (equipManager.utility11Spawner != null) SpawnForUtilitySpawner(equipManager.utility11Spawner, 1);
                if (equipManager.utility2Spawner != null) SpawnForUtilitySpawner(equipManager.utility2Spawner, 2);
                if (equipManager.utility22Spawner != null) SpawnForUtilitySpawner(equipManager.utility22Spawner, 2);

                PrepareAllVestSockets();
                SyncTabletDrones();
                MelonLoader.MelonLogger.Msg($"[Loadout] Synchronized {_activeDrones.Count} drones with RC Tablet on equip.");
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"Error in OnEquipButtonPressed: {ex}");
        }
    }

    /// <summary>
    /// Spawns a dedicated FPV equipment terminal in the briefing / armory room
    /// </summary>
    public static void SetupArmoryTerminal(Vector3 playerPosition, Quaternion playerRotation)
    {
        RCTablet.ResetPlayerFrozen();
        SetupGrenadeMenus();

        if (!Config.ModConfig.EnableArmoryTerminalStation)
        {
            if (_armoryTerminal != null)
            {
                UnityEngine.Object.Destroy(_armoryTerminal);
                _armoryTerminal = null;
            }
            return;
        }

        if (_armoryTerminal != null)
        {
            RespawnStationItems();
            return;
        }

        try
        {
            Vector3 terminalPos = playerPosition + playerRotation * new Vector3(1.2f, 0f, 1.0f);

            float floorY = 0.0f;
            if (Physics.Raycast(terminalPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.point.y < playerPosition.y - 0.7f)
                {
                    floorY = hit.point.y;
                }
                else
                {
                    floorY = Mathf.Max(0.0f, playerPosition.y - 1.65f);
                }
            }
            else
            {
                floorY = Mathf.Max(0.0f, playerPosition.y - 1.65f);
            }

            terminalPos.y = floorY;
            Quaternion terminalRot = playerRotation * Quaternion.Euler(0f, -45f, 0f);

            _terminalPos = terminalPos;
            _terminalRot = terminalRot;

            _armoryTerminal = new GameObject("FPV_Armory_Station");
            _armoryTerminal.transform.position = terminalPos;
            _armoryTerminal.transform.rotation = terminalRot;

            // 1. Station Desk (0.75m high, resting on floor)
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Station_Table";
            table.transform.SetParent(_armoryTerminal.transform, false);
            table.transform.localPosition = new Vector3(0f, 0.375f, 0f);
            table.transform.localScale = new Vector3(0.9f, 0.75f, 0.5f);
            SetColor(table, new Color(0.18f, 0.20f, 0.22f));

            // 2. Display Drone on table top
            Vector3 droneRestPos = terminalPos + terminalRot * new Vector3(-0.2f, 0.82f, 0f);
            _stationDrone = DroneFactory.CreateDrone(droneRestPos, terminalRot);
            _stationDrone.name = "Station_Drone_Display";

            // 3. Display Tablet on table top
            Vector3 tabletRestPos = terminalPos + terminalRot * new Vector3(0.2f, 0.79f, 0f);
            _stationTablet = DroneFactory.CreateRCTablet(tabletRestPos, terminalRot);
            _stationTablet.name = "Station_Tablet_Display";

            // Pair display drone and tablet
            var phys = _stationDrone.GetComponent<DronePhysics>();
            var cam = _stationDrone.GetComponent<DroneCameraRig>();
            var tab = _stationTablet.GetComponent<RCTablet>();
            if (phys != null && cam != null && tab != null)
            {
                phys.PowerOn();
            }

            // 4. Interactive "RESPAWN / EQUIP FPV" Button on table top
            CreateEquipButton(_armoryTerminal.transform, new Vector3(0f, 0.77f, 0.18f));

            // 5. Header Sign
            CreateHeaderSign(_armoryTerminal.transform, new Vector3(0f, 1.20f, 0f));

            MelonLoader.MelonLogger.Msg($"[Loadout] Spawned FPV Armory Station on floor at {terminalPos} (floorY={floorY})");
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"Failed to spawn Armory terminal: {ex.Message}");
        }
    }

    public static void RespawnStationItems()
    {
        try
        {
            if (_armoryTerminal == null || !_armoryTerminal) return;

            if (_stationDrone == null || !_stationDrone)
            {
                Vector3 droneRestPos = _terminalPos + _terminalRot * new Vector3(-0.2f, 0.82f, 0f);
                _stationDrone = DroneFactory.CreateDrone(droneRestPos, _terminalRot);
                _stationDrone.name = "Station_Drone_Display";
                MelonLoader.MelonLogger.Msg("[Loadout] Respawned fresh Drone on Armory Terminal.");
            }

            if (_stationTablet == null || !_stationTablet)
            {
                Vector3 tabletRestPos = _terminalPos + _terminalRot * new Vector3(0.2f, 0.79f, 0f);
                _stationTablet = DroneFactory.CreateRCTablet(tabletRestPos, _terminalRot);
                _stationTablet.name = "Station_Tablet_Display";
                MelonLoader.MelonLogger.Msg("[Loadout] Respawned fresh Tablet on Armory Terminal.");
            }

            if (_stationDrone != null && _stationDrone)
            {
                var phys = _stationDrone.GetComponent<DronePhysics>();
                if (phys != null)
                {
                    phys.PowerOn();
                    MelonLoader.MelonLogger.Msg("[Loadout] Successfully powered on station display drone!");
                }
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"Error in RespawnStationItems: {ex.Message}");
        }
    }

    private static void CreateEquipButton(Transform parent, Vector3 localPos)
    {
        GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btnObj.name = "Equip_FPV_Button";
        btnObj.transform.SetParent(parent, false);
        btnObj.transform.localPosition = localPos;
        btnObj.transform.localRotation = Quaternion.identity;
        btnObj.transform.localScale = new Vector3(0.18f, 0.04f, 0.18f);
        SetColor(btnObj, new Color(0.1f, 0.75f, 0.2f));

        Rigidbody rb = btnObj.AddComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        btnObj.AddComponent<ArmoryEquipButtonTrigger>();

        GameObject textObj = new GameObject("Btn_Text");
        textObj.transform.SetParent(btnObj.transform, false);
        textObj.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        textObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        textObj.transform.localScale = Vector3.one * 0.02f;
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = "RESPAWN / EQUIP";
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.black;
    }

    private static void CreateHeaderSign(Transform parent, Vector3 localPos)
    {
        GameObject signObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        signObj.name = "Station_Sign";
        signObj.transform.SetParent(parent, false);
        signObj.transform.localPosition = localPos;
        signObj.transform.localScale = new Vector3(0.7f, 0.15f, 0.02f);
        SetColor(signObj, new Color(0.1f, 0.1f, 0.12f));

        GameObject textObj = new GameObject("Sign_Text");
        textObj.transform.SetParent(signObj.transform, false);
        textObj.transform.localPosition = new Vector3(0f, 0f, -0.6f);
        textObj.transform.localScale = Vector3.one * 0.02f;
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = "FPV STRIKE DRONE STATION";
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.white;
    }

    /// <summary>
    /// Equips Drone & Tablet to the player's vest grenade pouches (utility1 and utility2)
    /// </summary>
    public static void EquipToManagerSockets(EquipButtonManager equipManager)
    {
        if (equipManager == null) return;
        if (CurrentLoadoutMode == FPVLoadoutMode.None) return;

        _activeDrones.Clear();
        if (equipManager.utility1Spawner != null) SpawnForUtilitySpawner(equipManager.utility1Spawner, 1);
        if (equipManager.utility11Spawner != null) SpawnForUtilitySpawner(equipManager.utility11Spawner, 1);
        if (equipManager.utility2Spawner != null) SpawnForUtilitySpawner(equipManager.utility2Spawner, 2);
        if (equipManager.utility22Spawner != null) SpawnForUtilitySpawner(equipManager.utility22Spawner, 2);
        SyncTabletDrones();
    }

    public static void EquipToPlayerSockets(PlayerHealth player = null)
    {
        RespawnStationItems();
        EquipToSceneSockets();
    }

    public static void EquipToSceneSockets()
    {
        if (CurrentLoadoutMode == FPVLoadoutMode.None) return;

        // Clean up destroyed drone references first
        for (int i = _activeDrones.Count - 1; i >= 0; i--)
        {
            if (_activeDrones[i] == null) _activeDrones.RemoveAt(i);
        }

        // If all 3 drones and tablet are already alive, do not re-equip destructively
        if (_activeDrones.Count >= 3 && _activeEquippedTablet != null)
        {
            MelonLoader.MelonLogger.Msg($"[Loadout] EquipToSceneSockets: All {_activeDrones.Count} drones and tablet active, syncing only.");
            PrepareAllVestSockets();
            SyncTabletDrones();
            return;
        }

        _spawnerCooldowns.Clear();
        WeaponAttachmentsSpawner[] spawners = UnityEngine.Object.FindObjectsOfType<WeaponAttachmentsSpawner>();
        if (spawners != null)
        {
            foreach (var sp in spawners)
            {
                if (sp == null || sp.HVRSocket == null) continue;
                if (sp.utility1) SpawnForUtilitySpawner(sp, 1);
                else if (sp.utility2) SpawnForUtilitySpawner(sp, 2);
            }
        }
        SyncTabletDrones();
    }

    private static void EquipToResolvedSockets(HVRSocket droneSocket, HVRSocket tabletSocket)
    {
        try
        {
            MelonLoader.MelonLogger.Msg($"[Loadout] Equipping FPV Kit into Grenade Pouches. DroneSocket='{droneSocket?.gameObject?.name}', TabletSocket='{tabletSocket?.gameObject?.name}'");

            if (_activeEquippedDrone != null) UnityEngine.Object.Destroy(_activeEquippedDrone);
            if (_activeEquippedTablet != null) UnityEngine.Object.Destroy(_activeEquippedTablet);
            _activeDrones.Clear();

            Vector3 spawnBase = droneSocket != null ? droneSocket.transform.position : (tabletSocket != null ? tabletSocket.transform.position : Vector3.zero);
            if (spawnBase == Vector3.zero)
            {
                PlayerHealth player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
                if (player != null)
                {
                    spawnBase = player.transform.position + player.transform.forward * 0.5f + Vector3.up * 0.8f;
                }
            }

            _activeEquippedDrone = DroneFactory.CreateDrone(spawnBase + Vector3.up * 0.05f, Quaternion.identity);
            _activeEquippedTablet = DroneFactory.CreateRCTablet(spawnBase + Vector3.up * 0.15f, Quaternion.identity);
            RegisterActiveDrone(_activeEquippedDrone);

            DronePhysics physics = _activeEquippedDrone.GetComponent<DronePhysics>();
            DroneCameraRig camRig = _activeEquippedDrone.GetComponent<DroneCameraRig>();
            RCTablet rcTablet = _activeEquippedTablet.GetComponent<RCTablet>();
            HVRGrabbable droneGrabbable = _activeEquippedDrone.GetComponent<HVRGrabbable>();
            HVRGrabbable tabletGrabbable = _activeEquippedTablet.GetComponent<HVRGrabbable>();

            if (physics != null && camRig != null && rcTablet != null)
            {
                PrepareAllVestSockets();
                SyncTabletDrones();
            }

            // Socket Drone into Vest Grenade Pouch 1 (utility1)
            if (droneSocket != null && droneGrabbable != null)
            {
                PrepareSocketForGrabbable(droneSocket);
                droneSocket.TryGrab(droneGrabbable, true, true);
                MelonLoader.MelonLogger.Msg($"[Loadout] FPV Drone holstered into vest grenade socket 1: {droneSocket.gameObject.name}");
            }

            // Socket Tablet into Vest Grenade Pouch 2 (utility2)
            if (tabletSocket != null && tabletGrabbable != null)
            {
                PrepareSocketForGrabbable(tabletSocket);
                tabletSocket.TryGrab(tabletGrabbable, true, true);
                MelonLoader.MelonLogger.Msg($"[Loadout] RC Tablet holstered into vest grenade socket 2: {tabletSocket.gameObject.name}");
            }

            if (CurrentLoadoutMode == FPVLoadoutMode.None)
            {
                CurrentLoadoutMode = FPVLoadoutMode.ControllerKit;
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"Error equipping resolved sockets: {ex}");
        }
    }

    private static readonly System.Collections.Generic.Dictionary<HVRSocket, Il2CppReferenceArray<HVRSocketFilter>> _savedSocketFilters = 
        new System.Collections.Generic.Dictionary<HVRSocket, Il2CppReferenceArray<HVRSocketFilter>>();

    public static int SavedSocketFiltersCount => _savedSocketFilters.Count;

    public static void RestoreAllSocketFilters()
    {
        foreach (var kvp in _savedSocketFilters)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.SocketFilters = kvp.Value;
            }
        }
        _savedSocketFilters.Clear();
    }

    /// <summary>
    /// Cleans up stale GameObject references and scene-bound sockets across scene transitions.
    /// Preserves CurrentLoadoutMode so the player's armory choice persists into mission scenes.
    /// </summary>
    public static void ClearSceneState()
    {
        try
        {
            _armoryTerminal = null;
            _stationDrone = null;
            _stationTablet = null;
            _activeEquippedDrone = null;
            _activeEquippedTablet = null;
            _activeDrones.Clear();
            RestoreAllSocketFilters();
            MelonLoader.MelonLogger.Msg("[Loadout] Scene state cleared: static GameObject references reset.");
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"[Loadout] ClearSceneState error: {ex.Message}");
        }
    }

    public static void PrepareSocketForGrabbable(HVRSocket socket)
    {
        if (socket == null) return;

        socket.CanRemoveGrabbable = true;
        socket.GrabbableMustBeHeld = false;
        socket.ParentDisablesGrab = false;
        socket.CheckHandOverlap = false; // Allow smooth natural placement into pouch
        socket.GrabsFromHand = false;    // NEVER snatch from hand! Only socket when player releases item.
        socket.GrabTimeout = 0.5f;       // Cooldown prevents immediate re-grab when taking item out of pouch

        // Preserve native socket filters instead of destroying them
        if (!_savedSocketFilters.ContainsKey(socket) && socket.SocketFilters != null)
        {
            _savedSocketFilters[socket] = socket.SocketFilters;
        }

        socket.SocketFilters = new Il2CppReferenceArray<HVRSocketFilter>(0);
    }

    public static void PrepareAllVestSockets()
    {
        try
        {
            var sockets = UnityEngine.Object.FindObjectsOfType<HVRSocket>();
            if (sockets != null)
            {
                foreach (var s in sockets)
                {
                    if (s == null) continue;
                    string name = s.gameObject.name.ToLower();
                    if (name.Contains("utility") || name.Contains("grenade") || name.Contains("holster") || name.Contains("pouch"))
                    {
                        PrepareSocketForGrabbable(s);
                    }
                }
            }
        }
        catch { }
    }

    private static void SetColor(GameObject obj, Color color)
    {
        if (obj == null) return;
        Renderer r = obj.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = DroneFactory.GetSharedMaterial(color);
        }
    }
}
