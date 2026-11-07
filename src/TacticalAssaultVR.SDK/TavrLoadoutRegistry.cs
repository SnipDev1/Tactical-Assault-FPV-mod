using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Il2Cpp;
using Il2CppHurricaneVR.Framework.Core.Grabbers;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TacticalAssaultVR.SDK;

public static class TavrLoadoutRegistry
{
    private static readonly Dictionary<string, TavrItemDef> _items = new Dictionary<string, TavrItemDef>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<WeaponAttachmentsSpawner, float> _spawnerCooldowns = new Dictionary<WeaponAttachmentsSpawner, float>();
    private static float _lastEquipTime = -999f;

    public static TavrItemDef Slot1Selection { get; private set; }
    public static TavrItemDef Slot2Selection { get; private set; }

    public static bool IsSlot1Custom => Slot1Selection != null;
    public static bool IsSlot2Custom => Slot2Selection != null;
    public static bool IsCustomActive => IsSlot1Custom || IsSlot2Custom;

    /// <summary>
    /// Registers a new custom weapon/gadget/utility with the centralized TAVR loadout system.
    /// Multiple mods can register independent items without conflicts.
    /// </summary>
    public static void RegisterItem(TavrItemDef item)
    {
        if (item == null || string.IsNullOrEmpty(item.Id)) throw new ArgumentException("Item or Item.Id cannot be null/empty");
        _items[item.Id] = item;
        MelonLoader.MelonLogger.Msg($"[TavrLoadoutRegistry] Registered custom item: '{item.Id}' ('{item.DisplayName}')");
    }

    /// <summary>
    /// Selects a registered item into Slot 1 or Slot 2.
    /// Leaves the other slot completely untouched.
    /// </summary>
    public static void SelectSlot(int slot, string itemId)
    {
        if (!_items.TryGetValue(itemId, out var item))
        {
            MelonLoader.MelonLogger.Warning($"[TavrLoadoutRegistry] Item '{itemId}' not found in registry.");
            return;
        }

        if (slot == 1)
        {
            Slot1Selection?.OnDeselected?.Invoke();
            Slot1Selection = item;
            item.OnSelected?.Invoke();
            UpdateMenuPreview(1, item);
        }
        else
        {
            Slot2Selection?.OnDeselected?.Invoke();
            Slot2Selection = item;
            item.OnSelected?.Invoke();
            UpdateMenuPreview(2, item);
        }

        MelonLoader.MelonLogger.Msg($"[TavrLoadoutRegistry] Slot {slot} set to '{item.DisplayName}'. (Slot1='{Slot1Selection?.DisplayName}', Slot2='{Slot2Selection?.DisplayName}')");
    }

    /// <summary>
    /// Clears custom selection for a slot, reverting that slot to vanilla game equipment.
    /// Leaves the other slot untouched.
    /// </summary>
    public static void ClearSlot(int slot)
    {
        if (slot == 1)
        {
            Slot1Selection?.OnDeselected?.Invoke();
            Slot1Selection = null;
        }
        else
        {
            Slot2Selection?.OnDeselected?.Invoke();
            Slot2Selection = null;
        }
        MelonLoader.MelonLogger.Msg($"[TavrLoadoutRegistry] Slot {slot} cleared to vanilla.");
    }

    private static void UpdateMenuPreview(int slot, TavrItemDef item)
    {
        try
        {
            if (slot == 1)
            {
                var u1 = UnityEngine.Object.FindObjectOfType<UtilityOneSelect>()
                      ?? Resources.FindObjectsOfTypeAll<UtilityOneSelect>().FirstOrDefault();
                if (u1 != null && u1.transform.childCount > 0)
                {
                    int idx = u1.transform.childCount - 1;
                    u1.selectedUtilityOne = idx;
                    SetPreviewText(u1.transform.GetChild(idx), item.DisplayName, item.DisplayColor);
                    u1.SelectWeapon();
                }
            }
            else
            {
                var u2 = UnityEngine.Object.FindObjectOfType<UtilityTwoSelect>()
                      ?? Resources.FindObjectsOfTypeAll<UtilityTwoSelect>().FirstOrDefault();
                if (u2 != null && u2.transform.childCount > 0)
                {
                    int idx = u2.transform.childCount - 1;
                    u2.selectedUtilityTwo = idx;
                    SetPreviewText(u2.transform.GetChild(idx), item.DisplayName, item.DisplayColor);
                    u2.SelectWeapon();
                }
            }
        }
        catch { }
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

    /// <summary>
    /// Injects gallery buttons for all registered mod items into the game's armory gallery menus.
    /// </summary>
    public static void SetupArmoryMenus()
    {
        try
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                if (go.name == "Utilities_slot1_Gallery") InjectGalleryButtons(go.transform, 1);
                else if (go.name == "Utilities_slot2_Gallery") InjectGalleryButtons(go.transform, 2);
            }

            // Expand attachments arrays on spawners
            EquipButtonManager equipMgr = UnityEngine.Object.FindObjectOfType<EquipButtonManager>();
            if (equipMgr != null)
            {
                ExpandSpawner(equipMgr.utility1Spawner);
                ExpandSpawner(equipMgr.utility11Spawner);
                ExpandSpawner(equipMgr.utility2Spawner);
                ExpandSpawner(equipMgr.utility22Spawner);
            }

            foreach (var sp in Resources.FindObjectsOfTypeAll<WeaponAttachmentsSpawner>())
            {
                if (sp != null && (sp.utility1 || sp.utility2 || sp.name.ToLower().Contains("utility")))
                {
                    ExpandSpawner(sp);
                }
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"[TavrLoadoutRegistry] SetupArmoryMenus error: {ex.Message}");
        }
    }

    private static void InjectGalleryButtons(Transform galleryTr, int slotNum)
    {
        if (galleryTr == null) return;
        Transform hg = galleryTr.Find("Gallery/HorizontalGroup (2)") ?? galleryTr.Find("Gallery/HorizontalGroup (1)");
        if (hg == null || hg.childCount == 0) return;

        Transform template = hg.GetChild(0);
        TavrSlotType currentSlotType = (slotNum == 1) ? TavrSlotType.UtilitySlot1 : TavrSlotType.UtilitySlot2;

        foreach (var kvp in _items)
        {
            var def = kvp.Value;
            if (!def.AllowedSlots.HasFlag(currentSlotType)) continue;

            string btnName = $"TAVR_SDK_Btn_{def.Id}_Slot{slotNum}";
            if (hg.Find(btnName) != null) continue;

            GameObject btn = GameObject.Instantiate(template.gameObject, hg, false);
            btn.name = btnName;

            Text txt = btn.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                txt.text = def.DisplayName;
                txt.color = def.DisplayColor;
            }

            Transform imgTr = btn.transform.Find("mask/image");
            if (imgTr != null)
            {
                Image img = imgTr.GetComponent<Image>();
                if (img != null) img.color = def.DisplayColor;
            }

            var mc = btn.GetComponent<MenuController>();
            if (mc != null && mc.buttonPressEvents != null)
            {
                mc.buttonPressEvents.RemoveAllListeners();
            }

            btn.SetActive(true);
            MelonLoader.MelonLogger.Msg($"[TavrLoadoutRegistry] Injected button '{btnName}' into '{galleryTr.name}'");
        }
    }

    private static void ExpandSpawner(WeaponAttachmentsSpawner spawner)
    {
        if (spawner == null || spawner.Attachments == null) return;
        if (spawner.Attachments.Length < 10 || spawner.Attachments.Length >= 16) return;

        int oldLen = spawner.Attachments.Length;
        var newArray = new Il2CppReferenceArray<GameObject>(oldLen + 1);
        for (int i = 0; i < oldLen; i++) newArray[i] = spawner.Attachments[i];
        newArray[oldLen] = spawner.Attachments[Mathf.Clamp(4, 0, oldLen - 1)];
        spawner.Attachments = newArray;
    }

    /// <summary>
    /// Handles gallery button clicks for SDK-registered items.
    /// Returns true if handled by SDK (suppressing vanilla click).
    /// </summary>
    public static bool HandleMenuButtonClick(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName) || !buttonName.StartsWith("TAVR_SDK_Btn_")) return false;

        // Button format: TAVR_SDK_Btn_{itemId}_Slot{slot}
        string withoutPrefix = buttonName.Substring("TAVR_SDK_Btn_".Length);
        int slotIdx = withoutPrefix.LastIndexOf("_Slot");
        if (slotIdx < 0) return false;

        string itemId = withoutPrefix.Substring(0, slotIdx);
        int slot = withoutPrefix.EndsWith("Slot1") ? 1 : 2;

        SelectSlot(slot, itemId);
        return true;
    }

    /// <summary>
    /// Called before vanilla WeaponAttachmentsSpawner.Selection runs.
    /// Returns false if this spawner is managed by a custom mod, suppressing vanilla grenade spawning.
    /// </summary>
    public static bool ShouldSuppressVanillaSelection(WeaponAttachmentsSpawner spawner)
    {
        if (spawner == null) return false;

        string sName = spawner.name.ToLower();
        bool isSlot1 = spawner.utility1 || sName.Contains("utility1");
        bool isSlot2 = spawner.utility2 || sName.Contains("utility2");

        if (isSlot1 && IsSlot1Custom) return true;
        if (isSlot2 && IsSlot2Custom) return true;

        return false;
    }

    /// <summary>
    /// Spawns custom items into a vest spawner socket if managed by a custom mod.
    /// </summary>
    public static void SpawnForSpawner(WeaponAttachmentsSpawner spawner, int slotNum)
    {
        if (spawner == null || spawner.HVRSocket == null) return;

        string sName = spawner.name.ToLower();
        bool isSlot1 = (slotNum == 1) || spawner.utility1 || sName.Contains("utility1");
        bool isSlot2 = (slotNum == 2) || spawner.utility2 || sName.Contains("utility2");
        bool isSecondary = sName.Contains("11") || sName.Contains("1_1") || sName.Contains("22") || sName.Contains("2_2");

        TavrItemDef item = isSlot1 ? Slot1Selection : (isSlot2 ? Slot2Selection : null);
        if (item == null || item.OnSpawn == null) return;

        // Allocation filter
        if (item.PouchAllocation == TavrPouchTarget.PrimaryPouch && isSecondary) return;
        if (item.PouchAllocation == TavrPouchTarget.SecondaryPouch && !isSecondary) return;

        // Debounce calls within 0.2s
        if (_spawnerCooldowns.TryGetValue(spawner, out float lastTime) && Time.time - lastTime < 0.2f) return;
        _spawnerCooldowns[spawner] = Time.time;

        HVRSocket socket = spawner.HVRSocket;
        TavrSocketHelper.PrepareVestSocket(socket);

        GameObject customObj = item.OnSpawn(socket, slotNum);
        if (customObj != null)
        {
            TavrSocketHelper.Holster(socket, customObj);
            item.OnHolstered?.Invoke(customObj, socket);
        }
    }

    /// <summary>
    /// Called when player clicks the armory EQUIP button.
    /// Equips active custom items into corresponding vest pouches.
    /// </summary>
    public static void OnEquipButtonPressed(EquipButtonManager equipManager)
    {
        if (equipManager == null || !IsCustomActive) return;
        if (Time.time - _lastEquipTime < 1.0f) return;
        _lastEquipTime = Time.time;

        _spawnerCooldowns.Clear();

        if (IsSlot1Custom)
        {
            if (equipManager.utility1Spawner != null) SpawnForSpawner(equipManager.utility1Spawner, 1);
            if (equipManager.utility11Spawner != null) SpawnForSpawner(equipManager.utility11Spawner, 1);
        }
        if (IsSlot2Custom)
        {
            if (equipManager.utility2Spawner != null) SpawnForSpawner(equipManager.utility2Spawner, 2);
            if (equipManager.utility22Spawner != null) SpawnForSpawner(equipManager.utility22Spawner, 2);
        }

        TavrSocketHelper.PrepareAllVestSockets();
    }

    /// <summary>
    /// Equips custom items when entering a mission scene or after player respawn.
    /// </summary>
    public static void EquipToSceneSockets()
    {
        if (!IsCustomActive) return;

        _spawnerCooldowns.Clear();
        var spawners = UnityEngine.Object.FindObjectsOfType<WeaponAttachmentsSpawner>();
        if (spawners == null) return;

        foreach (var sp in spawners)
        {
            if (sp == null || sp.HVRSocket == null) continue;
            string sName = sp.name.ToLower();
            if (sp.utility1 || sName.Contains("utility1"))
            {
                if (IsSlot1Custom) SpawnForSpawner(sp, 1);
            }
            else if (sp.utility2 || sName.Contains("utility2"))
            {
                if (IsSlot2Custom) SpawnForSpawner(sp, 2);
            }
        }

        TavrSocketHelper.PrepareAllVestSockets();
    }

    /// <summary>
    /// Clears scene-specific caches on scene transition while preserving player's loadout choices.
    /// </summary>
    public static void ClearSceneState()
    {
        _spawnerCooldowns.Clear();
        TavrSocketHelper.RestoreAllSocketFilters();
    }
}
