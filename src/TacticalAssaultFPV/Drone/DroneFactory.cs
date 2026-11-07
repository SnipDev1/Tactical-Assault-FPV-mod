using System;
using UnityEngine;
using Il2Cpp;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppHurricaneVR.Framework.Core.HandPoser;
using Il2CppHurricaneVR.Framework.Core.Sockets;
using Il2CppHurricaneVR.Framework.Shared;
using TacticalAssaultFPV.Controller;
using TacticalAssaultFPV.Config;

namespace TacticalAssaultFPV.Drone;

public static class DroneFactory
{
    public static GameObject CompetitionTimerPrefab { get; set; }

    public static GameObject CreateDrone(Vector3 position, Quaternion rotation)
    {
        // Clone from native working Shoulder Utility (SledgeHammer)
        GameObject basePrefab = Resources.Load<GameObject>("Weapons/ShoulderUtilities/SledgeHammer");
        if (basePrefab == null)
        {
            MelonLoader.MelonLogger.Error("[DroneFactory] Could not load SledgeHammer prefab from Resources!");
            return null;
        }

        GameObject drone = GameObject.Instantiate(basePrefab, position, rotation);
        drone.name = "FPV_Kamikaze_Drone";

        // 1. Remove Sledgehammer-specific damage, socket restrictions, and multiplayer components
        var sledgeDamage = drone.GetComponent<Il2Cpp.SledgeHammerDestroy>();
        if (sledgeDamage != null)
        {
            UnityEngine.Object.Destroy(sledgeDamage);
        }

        foreach (var comp in drone.GetComponents<Component>())
        {
            if (comp == null) continue;
            string t = comp.GetIl2CppType().FullName;
            if (t.Contains("SmoothSyncPUN2") ||
                t.Contains("NetworkPhysics") ||
                t.Contains("NetworkVisibilitySwitch") ||
                t.Contains("NetworkOwnershipTransfer") ||
                t.Contains("NetworkRoomCleanup") ||
                t.Contains("PhotonView") ||
                t.Contains("GrabbableLayerModifier") ||
                t.Contains("SledgeHammerDestroy") ||
                t.Contains("SledgeHammer"))
            {
                try { UnityEngine.Object.DestroyImmediate(comp); }
                catch { UnityEngine.Object.Destroy(comp); }
            }
        }

        // 2. Hide visual renderers of the original sledgehammer
        foreach (var r in drone.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.enabled = false;
        }

        // 3. Remove all original sledgehammer colliders so physical hull is not an invisible 0.8m sledgehammer (D4)
        foreach (var col in drone.GetComponentsInChildren<Collider>(true))
        {
            try
            {
                UnityEngine.Object.DestroyImmediate(col);
            }
            catch
            {
                UnityEngine.Object.Destroy(col);
            }
        }

        // Dedicated BoxCollider for the drone physical body
        var rootCol = drone.AddComponent<BoxCollider>();
        rootCol.center = new Vector3(0f, -0.012f, 0f);
        rootCol.size = new Vector3(0.286f, 0.116f, 0.256f);

        // 4. Attach custom visual FPV Drone model (from embedded resources)
        GameObject visualRoot = new GameObject("Drone_Visuals");
        visualRoot.transform.SetParent(drone.transform, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;

        // Frame body (chassis, arms, standoffs, camera mount, battery, motor bases)
        GameObject body = new GameObject("Frame_Body");
        body.transform.SetParent(visualRoot.transform, false);
        var bodyFilter = body.AddComponent<MeshFilter>();
        bodyFilter.sharedMesh = DroneModelAssets.BodyMesh;
        var bodyRenderer = body.AddComponent<MeshRenderer>();
        bodyRenderer.sharedMaterial = DroneModelAssets.DroneMaterial;

        // 4 Propellers (mounted on motor shafts at ±0.1178m X, +0.0261m Y, ±0.0973m Z)
        Vector3[] propPositions = new Vector3[]
        {
            new Vector3( 0.1178f, 0.0261f,  0.0973f), // Front-Right
            new Vector3(-0.1178f, 0.0261f,  0.0973f), // Front-Left
            new Vector3( 0.1178f, 0.0261f, -0.0973f), // Rear-Right
            new Vector3(-0.1178f, 0.0261f, -0.0973f)  // Rear-Left
        };
        Transform[] props = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject prop = new GameObject($"Propeller_{i}");
            prop.transform.SetParent(visualRoot.transform, false);
            prop.transform.localPosition = propPositions[i];
            prop.transform.localRotation = Quaternion.identity;
            var filter = prop.AddComponent<MeshFilter>();
            filter.sharedMesh = DroneModelAssets.PropMesh;
            var renderer = prop.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = DroneModelAssets.DroneMaterial;
            props[i] = prop.transform;
        }

        // Warhead payload (Contact bomb mounted under drone belly)
        GameObject warhead = new GameObject("Warhead_Charge");
        warhead.transform.SetParent(visualRoot.transform, false);
        warhead.transform.localPosition = Vector3.zero;
        warhead.transform.localRotation = Quaternion.identity;
        var bombFilter = warhead.AddComponent<MeshFilter>();
        bombFilter.sharedMesh = DroneModelAssets.BombMesh;
        var bombRenderer = warhead.AddComponent<MeshRenderer>();
        bombRenderer.sharedMaterial = DroneModelAssets.BombMaterial;

        // Status Power LED (unlit dark until powered on by pilot trigger)
        GameObject led = new GameObject("Status_LED");
        led.transform.SetParent(visualRoot.transform, false);
        led.transform.localPosition = Vector3.zero;
        led.transform.localRotation = Quaternion.identity;
        var ledFilter = led.AddComponent<MeshFilter>();
        ledFilter.sharedMesh = DroneModelAssets.LampMesh;
        var ledRenderer = led.AddComponent<MeshRenderer>();
        ledRenderer.sharedMaterial = DroneModelAssets.LampMaterial;

        // 5. Attach physical acoustic model FPV motor whine + prop wash
        AudioSource motorAudio = DroneMotorAudio.CreateSource(drone, DroneMotorAudio.MotorClip, 0.75f);

        // 6. Attach Drone flight, explosion, and camera logic
        DronePhysics physics = drone.AddComponent<DronePhysics>();
        physics.Propellers = props;
        physics.MotorAudio = motorAudio;
        physics.StatusLED = led;

        drone.AddComponent<DroneExplosive>();
        drone.AddComponent<DroneCameraRig>();

        // 7. Tune native HVRGrabbable and align HVRPosableGrabPoint to drone body center (D5)
        Transform grabPointsRoot = drone.transform.Find("GrabPoints");
        if (grabPointsRoot != null)
        {
            grabPointsRoot.localPosition = new Vector3(0f, -0.02f, 0f);
            grabPointsRoot.localRotation = Quaternion.identity;
        }

        var grabPoints = drone.GetComponentsInChildren<HVRPosableGrabPoint>(true);
        if (grabPoints != null)
        {
            foreach (var gp in grabPoints)
            {
                if (gp != null)
                {
                    gp.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                    gp.transform.localRotation = Quaternion.identity;
                }
            }
        }

        var grabbable = drone.GetComponent<HVRGrabbable>();
        if (grabbable != null)
        {
            grabbable.BreakDistanceSource = BreakDistanceSource.NoDistanceCheck;
            grabbable.RequireOverlapClearance = false;
            grabbable.GrabControl = HVRGrabControls.GripOnly;
            grabbable.HoldType = HVRHoldType.Swap;
            grabbable.CanBeGrabbed = true;
            grabbable.DisableHandGrab = false;

            // Re-bind HurricaneVR collider list to new BoxCollider
            grabbable.GrabColliders = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider>(new Collider[] { rootCol });
            grabbable.SetupColliders();
        }

        var rb = drone.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = ModConfig.DroneMassKg > 0.05f ? ModConfig.DroneMassKg : 0.65f;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // 8. Attach HVRSocketable so drone can be holstered back into vest grenade pouches
        var socketable = drone.GetComponent<HVRSocketable>() ?? drone.AddComponent<HVRSocketable>();
        socketable.SocketScale = 1.0f;
        socketable.CounterScale = Vector3.one;
        socketable.ScaleOverride = rootCol;
        socketable.SocketOrientation = drone.transform;
        socketable.Grabbable = grabbable;
        if (grabbable != null)
        {
            grabbable.SetupColliders();
        }

        MelonLoader.MelonLogger.Msg("[DroneFactory] Successfully cloned FPV Drone with motor scream sound!");
        return drone;
    }

    public static GameObject CreateRCTablet(Vector3 position, Quaternion rotation)
    {
        // Clone from native working handheld device with perfected screen-holding hand poser
        GameObject basePrefab = CompetitionTimerPrefab;
        if (basePrefab == null)
        {
            EquipButtonManager equipMgr = UnityEngine.Object.FindObjectOfType<EquipButtonManager>();
            if (equipMgr != null && equipMgr.utility1Spawner != null && equipMgr.utility1Spawner.Attachments != null)
            {
                foreach (var att in equipMgr.utility1Spawner.Attachments)
                {
                    if (att != null && att.name.StartsWith("CompetitionTimer"))
                    {
                        basePrefab = att;
                        CompetitionTimerPrefab = att;
                        break;
                    }
                }
            }
        }
        if (basePrefab == null)
        {
            basePrefab = Resources.Load<GameObject>("Weapons/Utilities/CompetitionTimer") 
                      ?? Resources.Load<GameObject>("Weapons/Utilities/BreachCharge");
            if (basePrefab != null)
            {
                CompetitionTimerPrefab = basePrefab;
            }
        }
        if (basePrefab == null)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == "CompetitionTimer" && go.scene.name == null)
                {
                    basePrefab = go;
                    CompetitionTimerPrefab = go;
                    break;
                }
            }
        }

        if (basePrefab == null)
        {
            MelonLoader.MelonLogger.Error("[DroneFactory] Could not load base prefab for RC Tablet!");
            return null;
        }

        GameObject tablet = GameObject.Instantiate(basePrefab, position, rotation);
        tablet.name = "FPV_RC_Tablet";

        // 1. Remove multiplayer sync, timer, button event, and unneeded scripts (preserving HVRTagSocketable / HVRSocketable)
        foreach (var c in tablet.GetComponents<Component>())
        {
            if (c == null) continue;
            string t = c.GetIl2CppType().FullName;
            if (t.Contains("SmoothSyncPUN2") ||
                t.Contains("NetworkPhysics") ||
                t.Contains("NetworkVisibilitySwitch") ||
                t.Contains("NetworkOwnershipTransfer") ||
                t.Contains("NetworkRoomCleanup") ||
                t.Contains("PhotonView") ||
                t.Contains("GrabbableLayerModifier") ||
                t.Contains("HVRPrimaryButtonEvents") ||
                t.Contains("HVRTriggerButtonEvents") ||
                t.Contains("GameplayTips") ||
                t.Contains("CompetitionTimer") ||
                t.Contains("NameToString") ||
                t.Contains("GrenadeCook") ||
                t.Contains("GrenadeTimer") ||
                t.Contains("SphereCollider"))
            {
                try
                {
                    UnityEngine.Object.DestroyImmediate(c);
                }
                catch
                {
                    UnityEngine.Object.Destroy(c);
                }
            }
        }

        // 2. Destroy unnecessary child objects (stopwatch UI, sfx, sounds, canvas, pin), keeping GrabPoints for hand poser
        for (int i = tablet.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = tablet.transform.GetChild(i);
            if (child.name != "GrabPoints" && !child.name.ToLower().Contains("socket"))
            {
                try
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                catch
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        // 3. Hide visual renderers of any remaining original meshes
        foreach (var r in tablet.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.enabled = false;
        }

        // Align tablet layer with native Grabbable / Utility layer
        int grabbableLayer = LayerMask.NameToLayer("Grabbable");
        if (grabbableLayer == -1) grabbableLayer = LayerMask.NameToLayer("Utility");
        if (grabbableLayer == -1 && basePrefab != null) grabbableLayer = basePrefab.layer;
        if (grabbableLayer != -1)
        {
            tablet.layer = grabbableLayer;
        }

        // 4. Attach custom visual Tablet Case and Screen
        // In CompetitionTimer, GrabPoints are at pos=(0, -0.02, 0) rot=(89.97, 0, 0).
        // Aligning visualRoot facing forward and tilted 15 degrees towards player's eyes
        // places the screen unobstructed above the holding hand!
        GameObject visualRoot = new GameObject("Tablet_Visuals");
        visualRoot.transform.SetParent(tablet.transform, false);
        visualRoot.transform.localPosition = new Vector3(0f, 0.04f, 0.01f);
        visualRoot.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

        // Tablet body casing (18 cm wide, 12 cm high, 1.5 cm thick)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Tablet_Case";
        body.transform.SetParent(visualRoot.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.18f, 0.12f, 0.015f);
        SetColor(body, new Color(0.12f, 0.12f, 0.14f));
        RemoveCollider(body);

        // Tablet screen: Quad facing player (normal -Z in local space)
        GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screen.name = "Screen";
        screen.transform.SetParent(visualRoot.transform, false);
        screen.transform.localPosition = new Vector3(0f, 0f, -0.009f);
        screen.transform.localRotation = Quaternion.identity;
        screen.transform.localScale = new Vector3(0.16f, 0.10f, 1f);
        RemoveCollider(screen);

        Material screenMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture"));
        screenMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // Off so screen is visible from all angles without backface culling
        Color darkScreen = new Color(0.04f, 0.05f, 0.06f); // Sleek off-black LCD glass (never blinding white)
        screenMat.color = darkScreen;
        if (screenMat.HasProperty("_BaseColor")) screenMat.SetColor("_BaseColor", darkScreen);
        screen.GetComponent<Renderer>().material = screenMat;

        // 5. Ensure BoxCollider is present and wraps around tablet
        BoxCollider col = tablet.GetComponent<BoxCollider>() ?? tablet.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.04f, 0.01f);
        col.size = new Vector3(0.20f, 0.14f, 0.04f);
        col.isTrigger = false;
        col.enabled = true;
        if (grabbableLayer != -1)
        {
            col.gameObject.layer = grabbableLayer;
        }

        // 6. Attach RCTablet logic component
        tablet.AddComponent<RCTablet>();

        // 7. Tune native HVRGrabbable for reliable handling
        var grabbable = tablet.GetComponent<HVRGrabbable>();
        if (grabbable != null)
        {
            grabbable.BreakDistanceSource = BreakDistanceSource.NoDistanceCheck;
            grabbable.RequireOverlapClearance = false;
            grabbable.GrabControl = HVRGrabControls.GripOnly;
            grabbable.HoldType = HVRHoldType.Swap;
            grabbable.CanBeGrabbed = true;
            grabbable.DisableHandGrab = false;

            // Explicitly set GrabColliders so HurricaneVR HVRTriggerGrabbableBag knows this collider
            grabbable.GrabColliders = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider>(new Collider[] { col });
            grabbable.SetupColliders();
        }

        var rb = tablet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = 0.8f;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // 8. Attach and tune native HVRSocketable / HVRTagSocketable so tablet can be holstered back into vest grenade pouches
        var tagSocketable = tablet.GetComponent<HVRTagSocketable>();
        if (tagSocketable != null)
        {
            tagSocketable.AllowSocketing = true;
            tagSocketable.SocketScale = 1.0f;
            tagSocketable.CounterScale = Vector3.one;
            tagSocketable.ScaleOverride = col;
            tagSocketable.SocketOrientation = tablet.transform;
            tagSocketable.Grabbable = grabbable;
        }

        var socketable = tablet.GetComponent<HVRSocketable>() ?? tablet.AddComponent<HVRSocketable>();
        socketable.SocketScale = 1.0f;
        socketable.CounterScale = Vector3.one;
        socketable.ScaleOverride = col;
        socketable.SocketOrientation = tablet.transform;
        socketable.Grabbable = grabbable;

        if (grabbable != null)
        {
            grabbable.SetupColliders();
        }

        MelonLoader.MelonLogger.Msg($"[DroneFactory] Successfully cloned RC Tablet from '{basePrefab.name}' with native hand poser!");
        return tablet;
    }

    private static void RemoveCollider(GameObject obj)
    {
        if (obj == null) return;
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
        {
            try
            {
                UnityEngine.Object.DestroyImmediate(col);
            }
            catch
            {
                UnityEngine.Object.Destroy(col);
            }
        }
    }


    private static Material _bodyMat;
    private static Material _armMat;
    private static Material _motorMat;
    private static Material _propMat;
    private static Material _warheadMat;

    public static Material BodyMaterial => _bodyMat ??= GetSharedMaterial(new Color(0.15f, 0.15f, 0.15f));
    public static Material ArmMaterial => _armMat ??= GetSharedMaterial(new Color(0.2f, 0.2f, 0.2f));
    public static Material MotorMaterial => _motorMat ??= GetSharedMaterial(new Color(0.1f, 0.1f, 0.1f));
    public static Material PropMaterial => _propMat ??= GetSharedMaterial(new Color(0.7f, 0.7f, 0.7f, 0.8f));
    public static Material WarheadMaterial => _warheadMat ??= GetSharedMaterial(new Color(0.25f, 0.35f, 0.2f));

    private static readonly System.Collections.Generic.Dictionary<Color, Material> _sharedMaterialCache = new System.Collections.Generic.Dictionary<Color, Material>();

    public static Material GetSharedMaterial(Color color)
    {
        if (_sharedMaterialCache.TryGetValue(color, out var cached) && cached != null)
        {
            return cached;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader)
        {
            color = color,
            hideFlags = HideFlags.HideAndDontSave
        };
        _sharedMaterialCache[color] = mat;
        return mat;
    }

    private static void SetColor(GameObject obj, Color color)
    {
        if (obj == null) return;
        Renderer r = obj.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = GetSharedMaterial(color);
        }
    }
}
