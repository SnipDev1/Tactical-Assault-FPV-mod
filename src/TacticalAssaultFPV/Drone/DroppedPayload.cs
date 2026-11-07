using System;
using UnityEngine;
using Il2CppInterop.Runtime.Attributes;

namespace TacticalAssaultFPV.Drone;

public class DroppedPayload : MonoBehaviour
{
    public DroppedPayload(IntPtr ptr) : base(ptr) { }

    private DronePayloadType _type;
    private float _dropTime;
    private bool _hasDetonated;

    public const float FragFuseTime = 3.5f;
    public const float ArmingGracePeriod = 0.35f;

    public bool HasDetonated => _hasDetonated;
    public DronePayloadType PayloadType => _type;

    [HideFromIl2Cpp]
    public void Initialize(DronePayloadType type)
    {
        _type = type;
        _dropTime = Time.time;
        _hasDetonated = false;
    }

    public void Update()
    {
        if (_hasDetonated) return;

        // Frag grenade: detonates after standard 3.5s fuse
        if (_type == DronePayloadType.DropFrag)
        {
            if (Time.time >= _dropTime + FragFuseTime)
            {
                Detonate();
            }
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (_hasDetonated) return;

        // Impact grenade: detonates on collision after arming grace period
        if (_type == DronePayloadType.DropImpact)
        {
            if (Time.time >= _dropTime + ArmingGracePeriod)
            {
                Detonate();
            }
        }
    }

    public void Detonate()
    {
        if (_hasDetonated) return;
        _hasDetonated = true;

        Vector3 pos = transform.position;
        DroneExplosive.TriggerExplosionAt(pos);
        Destroy(gameObject);
    }
}
