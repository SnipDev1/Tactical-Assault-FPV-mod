using System;
using System.IO;
using UnityEngine;

namespace TacticalAssaultVR.SDK;

public static class TavrAssetHelper
{
    private const uint MeshMagic = 0x4853454D; // 'MESH'

    /// <summary>
    /// Loads a custom binary .mesh file stream and flags it to never be unloaded by Unity scene transitions.
    /// Format: [uint32 magic 'MESH', uint32 version, uint32 vertCount, uint32 idxCount,
    ///          vertices(3xf32)*vertCount, normals(3xf32)*vertCount, uvs(2xf32)*vertCount,
    ///          triangles(uint32)*idxCount]
    /// </summary>
    public static Mesh LoadMeshFromStream(Stream stream, string meshName = "CustomMesh")
    {
        if (stream == null) return null;

        try
        {
            using var reader = new BinaryReader(stream);
            uint magic = reader.ReadUInt32();
            if (magic != MeshMagic)
            {
                MelonLoader.MelonLogger.Error($"[TavrAssetHelper] Invalid magic in mesh stream '{meshName}' (0x{magic:X8} != 0x{MeshMagic:X8})");
                return null;
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
            mesh.name = meshName;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            // Crucial: protect mesh from being destroyed during SceneManager.LoadScene asset garbage collection
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;
            return mesh;
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"[TavrAssetHelper] Error parsing mesh '{meshName}': {ex}");
            return null;
        }
    }

    /// <summary>
    /// Loads a Texture2D from a byte stream (.png or .jpg) and protects it across scene unloads.
    /// </summary>
    public static Texture2D LoadTextureFromStream(Stream stream, string texName = "CustomTex")
    {
        if (stream == null) return null;

        try
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            byte[] bytes = ms.ToArray();

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            tex.name = texName;
            ImageConversion.LoadImage(tex, bytes);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;
            return tex;
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error($"[TavrAssetHelper] Error loading texture '{texName}': {ex}");
            return null;
        }
    }

    /// <summary>
    /// Creates a material with double-sided rendering and automatic shader fallback:
    /// Universal Render Pipeline/Lit -> Standard -> Diffuse.
    /// </summary>
    public static Material CreateMaterial(Texture2D texture = null, Color? color = null, bool doubleSided = true)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                     Shader.Find("Standard") ??
                     Shader.Find("Diffuse");
        var mat = new Material(shader);

        if (color.HasValue)
        {
            mat.color = color.Value;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color.Value);
        }

        if (texture != null)
        {
            mat.mainTexture = texture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        }

        if (doubleSided && mat.HasProperty("_Cull"))
        {
            mat.SetFloat("_Cull", 0f); // 0 = Off (Double-sided)
        }

        mat.hideFlags = HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;
        return mat;
    }

    /// <summary>
    /// Creates a procedural primitive visual GameObject with collider stripped.
    /// </summary>
    public static GameObject CreatePrimitiveVisual(PrimitiveType type, Material material, Vector3 scale, string name = "VisualPart")
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);
        go.transform.localScale = scale;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && material != null) mr.sharedMaterial = material;
        return go;
    }
}
