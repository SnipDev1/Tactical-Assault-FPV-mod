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
    public static Mesh ImpactGrenadeMesh => _impactMesh ??= ResolveGrenadeMesh("Impact", CreateFallbackImpactMesh);

    [HideFromIl2Cpp]
    public static Material ImpactGrenadeMaterial => _impactMat ??= ResolveGrenadeMaterial("Impact", new Color(0.28f, 0.32f, 0.22f));

    [HideFromIl2Cpp]
    public static Mesh FragGrenadeMesh => _fragMesh ??= ResolveGrenadeMesh("Frag", CreateFallbackFragMesh);

    [HideFromIl2Cpp]
    public static Material FragGrenadeMaterial => _fragMat ??= ResolveGrenadeMaterial("Frag", new Color(0.20f, 0.28f, 0.16f));

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

    private static Mesh ResolveGrenadeMesh(string keyword, Func<Mesh> fallback)
    {
        try
        {
            var equipMgr = UnityEngine.Object.FindObjectOfType<Il2Cpp.EquipButtonManager>();
            if (equipMgr != null && equipMgr.utility1Spawner?.Attachments != null)
            {
                foreach (var att in equipMgr.utility1Spawner.Attachments)
                {
                    if (att != null && att.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var mf = att.GetComponentInChildren<MeshFilter>(true);
                        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;
                    }
                }
            }

            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.scene.name == null && go.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var mf = go.GetComponentInChildren<MeshFilter>(true);
                    if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;
                }
            }
        }
        catch { }

        return fallback();
    }

    private static Material ResolveGrenadeMaterial(string keyword, Color fallbackColor)
    {
        try
        {
            var equipMgr = UnityEngine.Object.FindObjectOfType<Il2Cpp.EquipButtonManager>();
            if (equipMgr != null && equipMgr.utility1Spawner?.Attachments != null)
            {
                foreach (var att in equipMgr.utility1Spawner.Attachments)
                {
                    if (att != null && att.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var mr = att.GetComponentInChildren<MeshRenderer>(true);
                        if (mr != null && mr.sharedMaterial != null) return mr.sharedMaterial;
                    }
                }
            }
        }
        catch { }

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { color = fallbackColor };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fallbackColor);
        return mat;
    }

    private static Mesh CreateFallbackImpactMesh()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.localScale = new Vector3(0.06f, 0.08f, 0.06f);
        var mesh = go.GetComponent<MeshFilter>()?.sharedMesh;
        GameObject.DestroyImmediate(go);
        return mesh;
    }

    private static Mesh CreateFallbackFragMesh()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.transform.localScale = new Vector3(0.055f, 0.045f, 0.055f);
        var mesh = go.GetComponent<MeshFilter>()?.sharedMesh;
        GameObject.DestroyImmediate(go);
        return mesh;
    }
}
