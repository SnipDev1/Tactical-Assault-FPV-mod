using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppInterop.Runtime.Attributes;
using TacticalAssaultFPV.Config;

namespace TacticalAssaultFPV.Drone;

public class DroneExplosive : MonoBehaviour
{
    public DroneExplosive(IntPtr ptr) : base(ptr) { }

    private static AudioClip _cachedExplosionClip;

    private DronePhysics _physics;
    private bool _isArmed;
    private bool _hasDetonated;
    private float _armedTime;

    private DronePayloadType _payloadType = DronePayloadType.Kamikaze;
    private DropPayloadState _dropState = DropPayloadState.Loaded;
    private float _primeTime;

    private const float MinDetonationSpeed = 1.8f;

    [HideFromIl2Cpp]
    [method: HideFromIl2Cpp]
    internal event Action OnDetonated;

    public bool IsArmed => _isArmed;
    public bool HasDetonated => _hasDetonated;
    public DronePayloadType PayloadType => _payloadType;
    public DropPayloadState DropState => _dropState;

    [HideFromIl2Cpp]
    internal GameObject[] PayloadVisuals { get; set; }

    public void Awake()
    {
        _physics = GetComponent<DronePhysics>();
        _isArmed = false;
        _payloadType = DronePayloadType.Kamikaze;
        _dropState = DropPayloadState.Loaded;
    }

    public void SetPayloadType(DronePayloadType type)
    {
        _payloadType = type;
        _dropState = DropPayloadState.Loaded;
        UpdatePayloadVisuals();
    }

    public void UpdatePayloadVisuals()
    {
        if (PayloadVisuals == null) return;
        for (int i = 0; i < PayloadVisuals.Length; i++)
        {
            if (PayloadVisuals[i] != null)
            {
                bool active = (i == (int)_payloadType) && (_dropState != DropPayloadState.Dropped);
                PayloadVisuals[i].SetActive(active);
            }
        }
    }

    public void PrimeDrop()
    {
        if (_payloadType == DronePayloadType.Kamikaze) return;
        if (_dropState != DropPayloadState.Loaded) return;
        _dropState = DropPayloadState.Primed;
        _primeTime = Time.time;
        DroneMotorAudio.PlayPrimeSound(transform.position);
    }

    public void ReleasePayload()
    {
        if (_payloadType == DronePayloadType.Kamikaze) return;
        if (_dropState == DropPayloadState.Dropped) return;

        _dropState = DropPayloadState.Dropped;
        UpdatePayloadVisuals();
        SpawnDroppedGrenade();
        DroneMotorAudio.PlayDropSound(transform.position);
    }

    private void SpawnDroppedGrenade()
    {
        Vector3 spawnPos = transform.position + transform.rotation * new Vector3(0f, -0.04f, 0.02f);
        Quaternion spawnRot = transform.rotation;

        GameObject grenade = new GameObject($"Dropped_{_payloadType}");
        grenade.transform.position = spawnPos;
        grenade.transform.rotation = spawnRot;

        var rb = grenade.AddComponent<Rigidbody>();
        rb.mass = 0.45f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.1f;

        var droneRb = GetComponent<Rigidbody>();
        if (droneRb != null)
        {
            rb.linearVelocity = droneRb.linearVelocity;
            rb.angularVelocity = droneRb.angularVelocity + UnityEngine.Random.insideUnitSphere * 2.0f;
        }

        var col = grenade.AddComponent<SphereCollider>();
        col.radius = 0.035f;

        var filter = grenade.AddComponent<MeshFilter>();
        var renderer = grenade.AddComponent<MeshRenderer>();
        if (_payloadType == DronePayloadType.DropImpact)
        {
            filter.sharedMesh = DroneModelAssets.ImpactGrenadeMesh;
            renderer.sharedMaterial = DroneModelAssets.ImpactGrenadeMaterial;
        }
        else
        {
            filter.sharedMesh = DroneModelAssets.FragGrenadeMesh;
            renderer.sharedMaterial = DroneModelAssets.FragGrenadeMaterial;
        }

        var payload = grenade.AddComponent<DroppedPayload>();
        payload.Initialize(_payloadType);
    }

    public void Update()
    {
        if (_dropState == DropPayloadState.Primed)
        {
            if (Time.time > _primeTime + 1.2f)
            {
                _dropState = DropPayloadState.Loaded;
            }
        }
    }

    public void SetArmed(bool armed)
    {
        _isArmed = armed;
        if (_isArmed)
        {
            _armedTime = Time.time;
            MelonLoader.MelonLogger.Msg("[FPV Explosive] Warhead ARMED! Contact detonation active.");
        }
        else
        {
            MelonLoader.MelonLogger.Msg("[FPV Explosive] Warhead SAFE.");
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        // Only Kamikaze payload detonates on drone collision!
        if (_payloadType != DronePayloadType.Kamikaze) return;
        if (_hasDetonated || !_isArmed) return;

        // Safety 1: Never detonate while held in the player's hand or socketed in vest pouch
        var grabbable = GetComponent<HVRGrabbable>();
        if (grabbable != null && (grabbable.IsHandGrabbed || grabbable.IsSocketed))
        {
            return;
        }

        // Safety 2: Never detonate if drone flight motors are disarmed (e.g. resting on ground/spawn)
        if (_physics == null)
        {
            _physics = GetComponent<DronePhysics>();
        }
        if (_physics != null && !_physics.IsArmed)
        {
            return;
        }

        // Safety grace period of 1.0s after arming prevents instant detonation on drop
        if (Time.time < _armedTime + 1.0f) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        MelonLoader.MelonLogger.Msg($"[FPV Explosive] Collision registered! Relative speed = {impactSpeed:F2} m/s, Armed = {_isArmed}");

        if (impactSpeed >= MinDetonationSpeed)
        {
            Detonate();
        }
    }

    public void Detonate()
    {
        if (_hasDetonated) return;
        _hasDetonated = true;

        Vector3 explosionPos = transform.position;
        MelonLoader.MelonLogger.Msg($"[FPV Explosive] Detonating Impact Grenade warhead at {explosionPos}!");

        TriggerExplosionAt(explosionPos);
        OnDetonated?.Invoke();

        Destroy(gameObject);
    }

    public static void TriggerExplosionAt(Vector3 explosionPos, GameObject source = null)
    {
        PlayExplosionAudio(explosionPos);
        SpawnNativeExplosionFX(explosionPos);
        SpawnProceduralExplosionVFX(explosionPos);
        TriggerGameExplosion(explosionPos, source);
    }

    private static void SpawnProceduralExplosionVFX(Vector3 pos)
    {
        try
        {
            // 1. Dynamic Point Light Flash
            GameObject lightObj = new GameObject("Explosion_Flash_Light");
            lightObj.transform.position = pos;
            Light flash = lightObj.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1.0f, 0.85f, 0.5f);
            flash.range = 22.0f;
            flash.intensity = 8.0f;
            Destroy(lightObj, 0.30f);

            // 2. Expanding Fireball Shockwave Sphere
            GameObject fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fireball.name = "Explosion_Fireball";
            fireball.transform.position = pos;
            fireball.transform.localScale = Vector3.one * 0.4f;

            var col = fireball.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = fireball.GetComponent<Renderer>();
            if (r != null)
            {
                Material fireMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                fireMat.color = new Color(1.0f, 0.65f, 0.15f, 0.95f);
                if (fireMat.HasProperty("_BaseColor")) fireMat.SetColor("_BaseColor", new Color(1.0f, 0.65f, 0.15f, 0.95f));
                r.material = fireMat;
            }

            MelonLoader.MelonCoroutines.Start(AnimateFireball(fireball));
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"Procedural explosion VFX failed: {ex.Message}");
        }
    }

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private static System.Collections.IEnumerator AnimateFireball(GameObject fireball)
    {
        if (fireball == null) yield break;
        float elapsed = 0f;
        const float duration = 0.35f;
        Vector3 startScale = Vector3.one * 0.4f;
        Vector3 endScale = Vector3.one * 4.2f;
        Renderer r = fireball.GetComponent<Renderer>();

        while (elapsed < duration)
        {
            if (fireball == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            fireball.transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            if (r != null && r.material != null)
            {
                Color c = Color.Lerp(new Color(1f, 0.75f, 0.2f), new Color(0.18f, 0.04f, 0.02f), t);
                r.material.color = c;
            }
            yield return null;
        }

        if (fireball != null)
        {
            Destroy(fireball);
        }
    }

    private static void SpawnNativeExplosionFX(Vector3 pos)
    {
        try
        {
            // 1. Visual explosion particle effect (fire, shockwave, debris, smoke)
            GameObject vfxPrefab = Resources.Load<GameObject>("FX/ExplosionFX/ImpactGrenadeExplosion");
            if (vfxPrefab == null)
            {
                vfxPrefab = Resources.Load<GameObject>("FX/ExplosionFX/GrenadeExplosion");
            }

            if (vfxPrefab != null)
            {
                GameObject vfx = GameObject.Instantiate(vfxPrefab, pos, Quaternion.identity);
                Destroy(vfx, 6.0f);
                MelonLoader.MelonLogger.Msg("[FPV Explosive] Successfully spawned ImpactGrenadeExplosion VFX!");
            }
            else
            {
                MelonLoader.MelonLogger.Warning("[FPV Explosive] Could not load ImpactGrenadeExplosion VFX prefab!");
            }

            // 2. Exact Contact RGD / Impact Grenade kill radius prefab from game resources
            GameObject killPrefab = Resources.Load<GameObject>("Weapons/Utilities/ExplosionKillsRadius/KillRadius_ImpactGrenade");
            if (killPrefab == null)
            {
                killPrefab = Resources.Load<GameObject>("Weapons/Utilities/ExplosionKillsRadius/KillRadius_FragGrenade");
            }

            if (killPrefab != null)
            {
                GameObject kill = GameObject.Instantiate(killPrefab, pos, Quaternion.identity);
                Destroy(kill, 4.0f);
                MelonLoader.MelonLogger.Msg("[FPV Explosive] Successfully spawned KillRadius_ImpactGrenade prefab!");
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"Failed to spawn native explosion prefab: {ex.Message}");
        }

        // 3. Dynamic flash light illuminating surrounding environment
        try
        {
            GameObject flashLight = new GameObject("ExplosionFlash");
            flashLight.transform.position = pos;
            Light light = flashLight.AddComponent<Light>();
            light.color = new Color(1f, 0.7f, 0.3f);
            light.intensity = 15f;
            light.range = 25f;
            Destroy(flashLight, 0.25f);
        }
        catch { }
    }

    private static void PlayExplosionAudio(Vector3 pos)
    {
        try
        {
            AudioClip clipToPlay = _cachedExplosionClip;

            if (clipToPlay == null)
            {
                // 1. Direct lookup from native Impact Grenade prefab ('- AudioSources --/GrenadeSFX')
                GameObject impactPrefab = Resources.Load<GameObject>("Weapons/Utilities/Impact_Grenade");
                if (impactPrefab != null)
                {
                    AudioSource[] sources = impactPrefab.GetComponentsInChildren<AudioSource>(true);
                    foreach (var src in sources)
                    {
                        if (src != null && src.clip != null)
                        {
                            string sName = src.gameObject.name.ToLower();
                            string cName = src.clip.name.ToLower();
                            if (sName.Contains("sfx") || cName.Contains("fraggrenade") || cName.Contains("grenade"))
                            {
                                clipToPlay = src.clip;
                                break;
                            }
                        }
                    }
                }

                // 2. Fallback search strictly for grenade explosion clip (never C4/huge bomb)
                if (clipToPlay == null)
                {
                    var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                    foreach (var clip in allClips)
                    {
                        if (clip == null) continue;
                        string cName = clip.name.ToLower();
                        if (cName.Contains("fraggrenade_01") || cName.Contains("fraggrenade") || cName == "grenade_explosion")
                        {
                            clipToPlay = clip;
                            break;
                        }
                    }
                }

                if (clipToPlay != null)
                {
                    _cachedExplosionClip = clipToPlay;
                }
            }

            if (clipToPlay != null)
            {
                // Create dedicated explosion audio host so audio isn't cut off when drone is destroyed
                GameObject audioHost = new GameObject("FPV_Explosion_Audio");
                audioHost.transform.position = pos;

                AudioSource src = audioHost.AddComponent<AudioSource>();
                src.clip = clipToPlay;
                src.spatialBlend = 0.80f; // 80% 3D positioned, 20% direct stereo (guarantees player hears it clearly even far away!)
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 25.0f; // Full volume up to 25 meters!
                src.maxDistance = 350.0f;
                src.volume = 1.0f;
                src.Play();

                Destroy(audioHost, 5.0f);
                MelonLoader.MelonLogger.Msg($"[FPV Explosive] Played loud contact grenade explosion sound '{clipToPlay.name}' at {pos}!");
            }
            else
            {
                MelonLoader.MelonLogger.Warning("[FPV Explosive] Could not find impact grenade explosion clip!");
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"Audio clip lookup failed: {ex.Message}");
        }
    }

    private static void TriggerGameExplosion(Vector3 pos, GameObject source = null)
    {
        // Contact grenade explosion specifications: radius ~5.5m, max damage 250
        float explosionRadius = 5.5f;
        int maxDamage = 250;

        Collider[] hitColliders = Physics.OverlapSphere(pos, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
        var damagedEnemies = new HashSet<Il2CppEnemyAI.EnemyHealth>();
        var damagedPlayers = new HashSet<PlayerHealth>();

        // Sort colliders by ascending distance to explosion so closest hitboxes are evaluated first
        Array.Sort(hitColliders, (a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            float da = (a.transform.position - pos).sqrMagnitude;
            float db = (b.transform.position - pos).sqrMagnitude;
            return da.CompareTo(db);
        });

        foreach (Collider col in hitColliders)
        {
            if (col == null || (source != null && col.gameObject == source)) continue;

            float dist = Vector3.Distance(pos, col.transform.position);
            float falloff = Mathf.Clamp01(1f - (dist / explosionRadius));
            int dmg = Mathf.RoundToInt(maxDamage * falloff);

            // 1. Damage Enemy AI
            try
            {
                var enemyHealth = col.GetComponentInParent<Il2CppEnemyAI.EnemyHealth>();
                if (enemyHealth != null && damagedEnemies.Add(enemyHealth))
                {
                    enemyHealth.TakeDamage(pos, (col.transform.position - pos).normalized, dmg, col, source);
                }
            }
            catch { }

            // 2. Damage Player
            try
            {
                var playerHealth = col.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null && damagedPlayers.Add(playerHealth))
                {
                    playerHealth.BulletDamage(dmg);
                }
            }
            catch { }

            // 3. Physical explosion impulse on rigidbodies (tuned realistically to prevent cartoon ragdoll launching)
            var rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.AddExplosionForce(90f, pos, explosionRadius, 0.15f, ForceMode.Impulse);
            }
        }
    }
}
