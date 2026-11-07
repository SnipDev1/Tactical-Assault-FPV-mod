using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Il2CppInterop.Runtime.Attributes;

namespace TacticalAssaultFPV.Drone;

public static class DroneModelAssets
{
    private static Mesh _bodyMesh;
    private static Mesh _propMesh;
    private static Mesh _lampMesh;
    private static Mesh _bombMesh;

    private static Material _droneMaterial;
    private static Material _bombMaterial;
    private static Material _lampMaterial;

    private static Mesh _impactMesh;
    private static Material _impactMat;
    private static Mesh _fragMesh;
    private static Material _fragMat;

    private static GameObject _cachedImpactPrefab;
    private static GameObject _cachedFragPrefab;

    [HideFromIl2Cpp]
    public static Mesh BodyMesh => _bodyMesh ??= LoadMeshResource("drone_body.mesh");

    [HideFromIl2Cpp]
    public static Mesh PropMesh => _propMesh ??= LoadMeshResource("drone_prop.mesh");

    [HideFromIl2Cpp]
    public static Mesh LampMesh => _lampMesh ??= LoadMeshResource("drone_lamp.mesh");

    [HideFromIl2Cpp]
    public static Mesh BombMesh => _bombMesh ??= LoadMeshResource("drone_bomb.mesh");

    [HideFromIl2Cpp]
    public static Material DroneMaterial => _droneMaterial ??= CreateTexturedMaterial("drone_tex.png");

    [HideFromIl2Cpp]
    public static Material BombMaterial => _bombMaterial ??= CreateTexturedMaterial("bomb_tex.png");

    [HideFromIl2Cpp]
    public static Material LampMaterial => _lampMaterial ??= CreateLampMaterial();

    [HideFromIl2Cpp]
    public static Mesh ImpactGrenadeMesh => _impactMesh ??= CreateFallbackMesh("ImpactFallback");

    [HideFromIl2Cpp]
    public static Material ImpactGrenadeMaterial => _impactMat ??= GetSharedFallbackMaterial(new Color(0.28f, 0.32f, 0.22f));

    [HideFromIl2Cpp]
    public static Mesh FragGrenadeMesh => _fragMesh ??= CreateFallbackMesh("FragFallback");

    [HideFromIl2Cpp]
    public static Material FragGrenadeMaterial => _fragMat ??= GetSharedFallbackMaterial(new Color(0.20f, 0.28f, 0.16f));

    private static Mesh LoadMeshResource(string name)
    {
        try
        {
            using var stream = OpenResourceStream(name);
            if (stream == null)
            {
                MelonLoader.MelonLogger.Warning($"[DroneModelAssets] Resource '{name}' not found, generating fallback.");
                return CreateFallbackMesh(name);
            }

            using var reader = new BinaryReader(stream);
            uint magic = reader.ReadUInt32();
            if (magic != 0x4853454D) // 'MESH'
            {
                MelonLoader.MelonLogger.Error($"[DroneModelAssets] Invalid magic in '{name}'");
                return CreateFallbackMesh(name);
            }

            uint version = reader.ReadUInt32();
            int vertCount = (int)reader.ReadUInt32();
            int idxCount = (int)reader.ReadUInt32();

            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];

            for (int i = 0; i < vertCount; i++)
            {
                vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            }

            var triangles = new int[idxCount];
            for (int i = 0; i < idxCount; i++)
            {
                triangles[i] = (int)reader.ReadUInt32();
            }

            var mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"[DroneModelAssets] Failed to load mesh '{name}': {ex}");
            return CreateFallbackMesh(name);
        }
    }

    private static Material CreateTexturedMaterial(string texName)
    {
        var tex = LoadTextureResource(texName);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                     Shader.Find("Standard") ??
                     Shader.Find("Diffuse");
        var mat = new Material(shader);
        if (tex != null)
        {
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        }
        // Two-sided rendering so thin plates/propellers are visible from all angles
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        return mat;
    }

    private static Material CreateLampMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                     Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.color = new Color(0.12f, 0.12f, 0.12f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f));
        return mat;
    }

    private static Texture2D LoadTextureResource(string name)
    {
        try
        {
            using var stream = OpenResourceStream(name);
            if (stream == null) return null;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            byte[] bytes = ms.ToArray();

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            ImageConversion.LoadImage(tex, bytes);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"[DroneModelAssets] Failed to load texture '{name}': {ex}");
            return null;
        }
    }

    private static Stream OpenResourceStream(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream("TacticalAssaultFPV.Resources." + name);
        if (stream != null) return stream;

        // Fallback to disk if running unpacked or during development
        string localPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "TacticalAssaultFPV", "Resources", name);
        if (File.Exists(localPath)) return File.OpenRead(localPath);

        return null;
    }

    private static Mesh CreateFallbackMesh(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = go.GetComponent<MeshFilter>()?.sharedMesh;
        GameObject.DestroyImmediate(go);
        return mesh;
    }

    [HideFromIl2Cpp]
    public static GameObject CreateVisualGrenade(string keyword, string objName)
    {
        var prefab = FindGrenadePrefab(keyword);
        if (prefab != null)
        {
            try
            {
                var clone = UnityEngine.Object.Instantiate(prefab);
                clone.name = objName;

                // Strip rigidbodies, colliders, and gameplay scripts
                foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
                {
                    try { UnityEngine.Object.DestroyImmediate(rb); } catch { UnityEngine.Object.Destroy(rb); }
                }
                foreach (var col in clone.GetComponentsInChildren<Collider>(true))
                {
                    try { UnityEngine.Object.DestroyImmediate(col); } catch { UnityEngine.Object.Destroy(col); }
                }
                foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    try { UnityEngine.Object.DestroyImmediate(mb); } catch { UnityEngine.Object.Destroy(mb); }
                }

                // Enable renderers and make materials two-sided
                foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = true;
                    if (r.materials != null)
                    {
                        foreach (var mat in r.materials)
                        {
                            if (mat != null && mat.HasProperty("_Cull"))
                            {
                                mat.SetFloat("_Cull", 0f);
                            }
                        }
                    }
                }

                return clone;
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[DroneModelAssets] Failed to clone prefab '{keyword}': {ex.Message}. Falling back to procedural.");
            }
        }

        return CreateFallbackGrenade(keyword, objName);
    }

    private static GameObject FindGrenadePrefab(string keyword)
    {
        bool isImpact = keyword.IndexOf("Impact", StringComparison.OrdinalIgnoreCase) >= 0;
        if (isImpact && _cachedImpactPrefab != null) return _cachedImpactPrefab;
        if (!isImpact && _cachedFragPrefab != null) return _cachedFragPrefab;

        try
        {
            var equipMgr = UnityEngine.Object.FindObjectOfType<Il2Cpp.EquipButtonManager>();
            if (equipMgr != null && equipMgr.utility1Spawner?.Attachments != null)
            {
                foreach (var att in equipMgr.utility1Spawner.Attachments)
                {
                    if (att != null && att.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (isImpact) _cachedImpactPrefab = att;
                        else _cachedFragPrefab = att;
                        return att;
                    }
                }
            }

            string resPath = isImpact ? "Weapons/Utilities/Impact_Grenade" : "Weapons/Utilities/Frag_Grenade";
            var res = Resources.Load<GameObject>(resPath);
            if (res != null)
            {
                if (isImpact) _cachedImpactPrefab = res;
                else _cachedFragPrefab = res;
                return res;
            }

            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.scene.name == null && go.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (isImpact) _cachedImpactPrefab = go;
                    else _cachedFragPrefab = go;
                    return go;
                }
            }
        }
        catch { }

        return null;
    }

    private static GameObject CreateFallbackGrenade(string keyword, string objName)
    {
        bool isImpact = keyword.IndexOf("Impact", StringComparison.OrdinalIgnoreCase) >= 0;
        GameObject root = new GameObject(objName);

        // Body
        var body = GameObject.CreatePrimitive(isImpact ? PrimitiveType.Sphere : PrimitiveType.Capsule);
        body.name = "Grenade_Body";
        body.transform.SetParent(root.transform, false);
        var bodyCol = body.GetComponent<Collider>();
        if (bodyCol != null) UnityEngine.Object.DestroyImmediate(bodyCol);

        if (isImpact)
        {
            body.transform.localScale = new Vector3(0.065f, 0.08f, 0.065f);
            var mr = body.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = GetSharedFallbackMaterial(new Color(0.28f, 0.32f, 0.22f));
        }
        else
        {
            body.transform.localScale = new Vector3(0.055f, 0.045f, 0.055f);
            var mr = body.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = GetSharedFallbackMaterial(new Color(0.20f, 0.28f, 0.16f));
        }

        // Spoon (lever)
        var spoon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spoon.name = "Grenade_Spoon";
        spoon.transform.SetParent(root.transform, false);
        var spoonCol = spoon.GetComponent<Collider>();
        if (spoonCol != null) UnityEngine.Object.DestroyImmediate(spoonCol);
        spoon.transform.localScale = new Vector3(0.012f, 0.075f, 0.006f);
        spoon.transform.localPosition = new Vector3(0.032f, -0.005f, 0f);
        spoon.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        var spoonMr = spoon.GetComponent<MeshRenderer>();
        if (spoonMr != null) spoonMr.sharedMaterial = GetSharedFallbackMaterial(new Color(0.25f, 0.25f, 0.27f));

        // Fuze cap
        var fuze = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fuze.name = "Grenade_Fuze";
        fuze.transform.SetParent(root.transform, false);
        var fuzeCol = fuze.GetComponent<Collider>();
        if (fuzeCol != null) UnityEngine.Object.DestroyImmediate(fuzeCol);
        fuze.transform.localScale = new Vector3(0.018f, 0.012f, 0.018f);
        fuze.transform.localPosition = new Vector3(0f, 0.045f, 0f);
        var fuzeMr = fuze.GetComponent<MeshRenderer>();
        if (fuzeMr != null) fuzeMr.sharedMaterial = GetSharedFallbackMaterial(new Color(0.38f, 0.35f, 0.28f));

        return root;
    }

    private static Material GetSharedFallbackMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                     Shader.Find("Standard") ??
                     Shader.Find("Diffuse");
        var mat = new Material(shader) { color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        return mat;
    }
}
