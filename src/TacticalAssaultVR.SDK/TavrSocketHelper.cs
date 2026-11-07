using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppHurricaneVR.Framework.Core.Grabbers;
using Il2CppHurricaneVR.Framework.Core.Sockets;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TacticalAssaultVR.SDK;

public static class TavrSocketHelper
{
    private static readonly Dictionary<HVRSocket, Il2CppReferenceArray<HVRSocketFilter>> _savedSocketFilters = 
        new Dictionary<HVRSocket, Il2CppReferenceArray<HVRSocketFilter>>();

    /// <summary>
    /// Configures a vest grenade/utility socket for natural, smooth VR interaction:
    /// Never auto-snatches from player's hand; only holsters when released.
    /// Prevents instant re-grabbing with a 0.5s grab timeout.
    /// </summary>
    public static void PrepareVestSocket(HVRSocket socket)
    {
        if (socket == null) return;

        socket.CanRemoveGrabbable = true;
        socket.GrabbableMustBeHeld = false;
        socket.ParentDisablesGrab = false;
        socket.CheckHandOverlap = false; // Smooth natural release into pouch
        socket.GrabsFromHand = false;    // NEVER snatch from hand!
        socket.GrabTimeout = 0.5f;       // Cooldown prevents immediate re-grab

        // Preserve filters if not already saved
        if (!_savedSocketFilters.ContainsKey(socket) && socket.SocketFilters != null)
        {
            _savedSocketFilters[socket] = socket.SocketFilters;
        }

        // Allow custom items to be placed into this socket
        socket.SocketFilters = new Il2CppReferenceArray<HVRSocketFilter>(0);
    }

    /// <summary>
    /// Restores original game socket filters for all modified sockets.
    /// </summary>
    public static void RestoreAllSocketFilters()
    {
        foreach (var kvp in _savedSocketFilters)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                try { kvp.Key.SocketFilters = kvp.Value; } catch { }
            }
        }
        _savedSocketFilters.Clear();
    }

    /// <summary>
    /// Smoothly holsters a custom GameObject into a HurricaneVR socket.
    /// </summary>
    public static bool Holster(HVRSocket socket, GameObject item)
    {
        if (socket == null || item == null) return false;

        PrepareVestSocket(socket);

        var grabbable = item.GetComponent<HVRGrabbable>();
        if (grabbable != null)
        {
            bool grabbed = socket.TryGrab(grabbable, true, true);
            if (!grabbed)
            {
                item.transform.position = socket.transform.position;
                item.transform.rotation = socket.transform.rotation;
            }
            return grabbed;
        }

        item.transform.position = socket.transform.position;
        item.transform.rotation = socket.transform.rotation;
        return false;
    }

    /// <summary>
    /// Configures all vest grenade/utility sockets found in the scene.
    /// </summary>
    public static void PrepareAllVestSockets()
    {
        var sockets = UnityEngine.Object.FindObjectsOfType<HVRSocket>();
        if (sockets == null) return;

        foreach (var s in sockets)
        {
            if (s == null) continue;
            string sName = s.gameObject.name.ToLower();
            if (sName.Contains("utility") || sName.Contains("grenade") || sName.Contains("pouch"))
            {
                PrepareVestSocket(s);
            }
        }
    }
}
