using System;
using System.Collections;
using MelonLoader;
using UnityEngine;

namespace TacticalAssaultVR.SDK;

public abstract class TavrModBase : MelonMod
{
    public override void OnInitializeMelon()
    {
        base.OnInitializeMelon();
        MelonLogger.Msg($"[{Info.Name}] Initializing with TacticalAssaultVR.SDK v1.0.0");
        OnModInitialized();
    }

    /// <summary>
    /// Override to register custom loadout items, IL2CPP types, and assets.
    /// </summary>
    protected virtual void OnModInitialized() { }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        base.OnSceneWasLoaded(buildIndex, sceneName);
        TavrLoadoutRegistry.ClearSceneState();
        OnSceneLoaded(buildIndex, sceneName);
    }

    protected virtual void OnSceneLoaded(int buildIndex, string sceneName) { }

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        TavrSocketHelper.RestoreAllSocketFilters();
    }
}
