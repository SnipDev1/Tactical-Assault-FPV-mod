using System;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime.Injection;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppHurricaneVR.Framework.Core.Grabbers;
using TacticalAssaultVR.SDK;

[assembly: MelonInfo(typeof(SampleUtilityMod.SampleFlareMod), "SampleFlareMod", "1.0.0", "TAVR Modding Community")]
[assembly: MelonGame("Jarhead Games", "TacticalAssaultVR")]

namespace SampleUtilityMod;

public class SampleFlareMod : TavrModBase
{
    protected override void OnModInitialized()
    {
        ClassInjector.RegisterTypeInIl2Cpp<SignalFlareItem>();

        // Register Signal Flare with the SDK centralized loadout registry
        TavrLoadoutRegistry.RegisterItem(new TavrItemDef
        {
            Id = "sample.signal_flare",
            DisplayName = "SIGNAL FLARE",
            DisplayColor = new Color(1.0f, 0.85f, 0.2f), // Amber / Gold
            AllowedSlots = TavrSlotType.Both,
            PouchAllocation = TavrPouchTarget.BothPouches,
            OnSpawn = SpawnFlareItem
        });

        MelonLogger.Msg("[SampleFlareMod] Signal Flare registered with TavrLoadoutRegistry!");
    }

    private static GameObject SpawnFlareItem(HVRSocket socket, int slotNum)
    {
        Vector3 pos = socket != null ? socket.transform.position : Vector3.zero;
        Quaternion rot = socket != null ? socket.transform.rotation : Quaternion.identity;

        GameObject flare = new GameObject("Signal_Flare_Item");
        flare.transform.position = pos;
        flare.transform.rotation = rot;

        // 1. Physical body & collider
        var rb = flare.AddComponent<Rigidbody>();
        rb.mass = 0.35f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var col = flare.AddComponent<CapsuleCollider>();
        col.radius = 0.025f;
        col.height = 0.16f;

        // 2. Visuals (Body tube + Cap) using TavrAssetHelper
        var bodyMat = TavrAssetHelper.CreateMaterial(null, new Color(0.85f, 0.25f, 0.15f)); // Bright red-orange tube
        var capMat = TavrAssetHelper.CreateMaterial(null, new Color(0.2f, 0.2f, 0.22f));   // Metal cap

        var bodyObj = TavrAssetHelper.CreatePrimitiveVisual(PrimitiveType.Cylinder, bodyMat, new Vector3(0.045f, 0.07f, 0.045f), "Tube");
        bodyObj.transform.SetParent(flare.transform, false);

        var capObj = TavrAssetHelper.CreatePrimitiveVisual(PrimitiveType.Cylinder, capMat, new Vector3(0.048f, 0.015f, 0.048f), "Cap");
        capObj.transform.SetParent(flare.transform, false);
        capObj.transform.localPosition = new Vector3(0f, 0.07f, 0f);

        // 3. VR Grabbable
        var grabbable = flare.AddComponent<HVRGrabbable>();
        grabbable.GrabColliders = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider>(new Collider[] { col });

        // 4. Custom behavior
        flare.AddComponent<SignalFlareItem>();

        return flare;
    }
}

public class SignalFlareItem : MonoBehaviour
{
    public SignalFlareItem(IntPtr ptr) : base(ptr) { }

    private bool _isIgnited;
    private float _igniteTime;
    private HVRGrabbable _grabbable;

    public void Start()
    {
        _grabbable = GetComponent<HVRGrabbable>();
    }

    public void Update()
    {
        if (_isIgnited)
        {
            // Burn for 30 seconds
            if (Time.time > _igniteTime + 30.0f)
            {
                Destroy(gameObject);
            }
            return;
        }

        // Ignite when held and player pulls trigger or presses primary button
        if (_grabbable != null && _grabbable.IsHandGrabbed)
        {
            var handGrabber = _grabbable.PrimaryGrabber?.TryCast<HVRHandGrabber>();
            if (handGrabber != null)
            {
                if (TavrInputHelper.GetTrigger(handGrabber.HandSide) > 0.5f || 
                    TavrInputHelper.GetPrimaryButtonDown(handGrabber.HandSide))
                {
                    Ignite();
                }
            }
        }
    }

    public void Ignite()
    {
        if (_isIgnited) return;
        _isIgnited = true;
        _igniteTime = Time.time;

        MelonLogger.Msg("[SignalFlare] Flare ignited!");

        // Add flare light effect
        var lightObj = new GameObject("FlareLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.08f, 0f);

        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1.0f, 0.35f, 0.1f);
        light.range = 15.0f;
        light.intensity = 2.5f;
    }
}
