using System;
using UnityEngine;

namespace TacticalAssaultVR.SDK;

public static class TavrPrefabHelper
{
    /// <summary>
    /// Loads a base game prefab by resource path, or searches active EquipButtonManager attachments.
    /// </summary>
    public static GameObject FindGamePrefab(string resourcePath, string fallbackKeyword = null)
    {
        var res = Resources.Load<GameObject>(resourcePath);
        if (res != null) return res;

        if (!string.IsNullOrEmpty(fallbackKeyword))
        {
            var equipMgr = UnityEngine.Object.FindObjectOfType<Il2Cpp.EquipButtonManager>();
            if (equipMgr != null && equipMgr.utility1Spawner?.Attachments != null)
            {
                foreach (var att in equipMgr.utility1Spawner.Attachments)
                {
                    if (att != null && att.name.IndexOf(fallbackKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return att;
                    }
                }
            }

            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.scene.name == null && go.name.IndexOf(fallbackKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return go;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Removes unwanted gameplay, multiplayer, or weapon-specific components from an instantiated clone.
    /// </summary>
    public static void StripComponents(GameObject root, params string[] typeNameKeywords)
    {
        if (root == null || typeNameKeywords == null || typeNameKeywords.Length == 0) return;

        foreach (var comp in root.GetComponentsInChildren<Component>(true))
        {
            if (comp == null) continue;
            string tName = comp.GetIl2CppType().FullName;
            foreach (var kw in typeNameKeywords)
            {
                if (tName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try { UnityEngine.Object.DestroyImmediate(comp); }
                    catch { UnityEngine.Object.Destroy(comp); }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Clones a game prefab and strips all physics, colliders, and scripts, leaving a purely visual hierarchy.
    /// </summary>
    public static GameObject CloneAsVisual(GameObject prefab, string name = "VisualClone")
    {
        if (prefab == null) return null;

        var clone = UnityEngine.Object.Instantiate(prefab);
        clone.name = name;

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

        foreach (var mr in clone.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.enabled = true;
            if (mr.materials != null)
            {
                foreach (var mat in mr.materials)
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
}
