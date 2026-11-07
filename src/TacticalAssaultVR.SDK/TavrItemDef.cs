using System;
using UnityEngine;
using Il2CppHurricaneVR.Framework.Core.Grabbers;

namespace TacticalAssaultVR.SDK;

[Flags]
public enum TavrSlotType
{
    None = 0,
    UtilitySlot1 = 1, // Left pouches (utility1 & utility1_1)
    UtilitySlot2 = 2, // Right pouches (utility2 & utility2_2)
    Both = UtilitySlot1 | UtilitySlot2
}

public enum TavrPouchTarget
{
    PrimaryPouch = 0,   // First pouch of the slot (utility1 or utility2)
    SecondaryPouch = 1, // Second pouch of the slot (utility1_1 or utility2_2)
    BothPouches = 2     // Fills both pouches of the slot
}

public class TavrItemDef
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public Color DisplayColor { get; set; } = new Color(0.2f, 1.0f, 0.4f);
    public TavrSlotType AllowedSlots { get; set; } = TavrSlotType.Both;
    public TavrPouchTarget PouchAllocation { get; set; } = TavrPouchTarget.BothPouches;

    /// <summary>
    /// Factory function called to instantiate and return the custom GameObject for a given socket and slot number.
    /// Parameters: (HVRSocket socket, int slotNum)
    /// </summary>
    public Func<HVRSocket, int, GameObject> OnSpawn { get; set; }

    /// <summary>
    /// Optional callback when the item is holstered into a pouch.
    /// </summary>
    public Action<GameObject, HVRSocket> OnHolstered { get; set; }

    /// <summary>
    /// Optional callback when this item is selected in the armory loadout.
    /// </summary>
    public Action OnSelected { get; set; }

    /// <summary>
    /// Optional callback when this item is deselected in the armory loadout.
    /// </summary>
    public Action OnDeselected { get; set; }
}
