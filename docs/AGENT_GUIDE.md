# Tactical Assault VR (TAVR) Modding SDK — AI Agent Guide

Руководство для AI-агентов (Antigravity, Claude, ChatGPT) и разработчиков по созданию новых модов для **Tactical Assault VR** с использованием `TacticalAssaultVR.SDK`.

---

## 1. Архитектура и стек технологий

- **Движок**: Unity (Universal Render Pipeline, URP).
- **Среда выполнения**: Unity IL2CPP (C# транслирован в C++ native code).
- **Загрузчик модов**: [MelonLoader 0.6.x](https://melonloader.modstats.org/) (Target: `net6.0`).
- **VR фреймворк**: HurricaneVR (`Il2CppHurricaneVR.Framework`).
- **Сетевой стек**: Photon Unity Networking 2 (`PUN2`).

---

## 2. Быстрый старт: Создание нового мода за 1 шаг

### 2.1 Файл проекта `.csproj`
Создайте файл `MyMod.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
    <AssemblyName>MyMod</AssemblyName>
    <OutputPath>bin/$(Configuration)/</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../TacticalAssaultVR.SDK/TacticalAssaultVR.SDK.csproj" />
    <Reference Include="MelonLoader"><HintPath>$(GameDir)/MelonLoader/net6/MelonLoader.dll</HintPath></Reference>
    <Reference Include="Il2CppInterop.Runtime"><HintPath>$(GameDir)/MelonLoader/net6/Il2CppInterop.Runtime.dll</HintPath></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$(GameDir)/MelonLoader/Il2CppAssemblies/UnityEngine.CoreModule.dll</HintPath></Reference>
    <Reference Include="UnityEngine.PhysicsModule"><HintPath>$(GameDir)/MelonLoader/Il2CppAssemblies/UnityEngine.PhysicsModule.dll</HintPath></Reference>
    <Reference Include="Assembly-CSharp"><HintPath>$(GameDir)/MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll</HintPath></Reference>
    <Reference Include="Il2CppHurricaneVR.Framework"><HintPath>$(GameDir)/MelonLoader/Il2CppAssemblies/Il2CppHurricaneVR.Framework.dll</HintPath></Reference>
  </ItemGroup>
</Project>
```

### 2.2 Код мода (`MyMod.cs`)
```csharp
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime.Injection;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppHurricaneVR.Framework.Core.Grabbers;
using TacticalAssaultVR.SDK;

[assembly: MelonInfo(typeof(MyMod.Main), "MyMod", "1.0.0", "Author")]
[assembly: MelonGame("Jarhead Games", "TacticalAssaultVR")]

namespace MyMod;

public class Main : TavrModBase
{
    protected override void OnModInitialized()
    {
        ClassInjector.RegisterTypeInIl2Cpp<MyGadgetItem>();

        // Регистрация предмета в едином реестре экипировки SDK:
        TavrLoadoutRegistry.RegisterItem(new TavrItemDef
        {
            Id = "mymod.gadget",
            DisplayName = "TACTICAL SCANNER",
            DisplayColor = new Color(0.2f, 0.8f, 1.0f),
            AllowedSlots = TavrSlotType.Both,            // Доступен в обоих слотах меню
            PouchAllocation = TavrPouchTarget.BothPouches, // Заполняет карманы слота
            OnSpawn = (socket, slotNum) => SpawnGadget(socket.transform.position, socket.transform.rotation)
        });
    }

    private static GameObject SpawnGadget(Vector3 pos, Quaternion rot)
    {
        var go = new GameObject("Tactical_Scanner");
        go.transform.position = pos;
        go.transform.rotation = rot;

        // Физика
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.08f, 0.14f, 0.03f);

        // Визуал через TavrAssetHelper
        var mat = TavrAssetHelper.CreateMaterial(null, Color.cyan);
        var visual = TavrAssetHelper.CreatePrimitiveVisual(PrimitiveType.Cube, mat, col.size);
        visual.transform.SetParent(go.transform, false);

        // Интеграция с HurricaneVR (хват рукой)
        var grabbable = go.AddComponent<HVRGrabbable>();
        grabbable.GrabColliders = new(new Collider[] { col });

        // Логика
        go.AddComponent<MyGadgetItem>();
        return go;
    }
}

public class MyGadgetItem : MonoBehaviour
{
    public MyGadgetItem(IntPtr ptr) : base(ptr) { }

    private HVRGrabbable _grabbable;

    public void Start() => _grabbable = GetComponent<HVRGrabbable>();

    public void Update()
    {
        if (_grabbable != null && _grabbable.IsHandGrabbed)
        {
            var hand = _grabbable.PrimaryGrabber?.TryCast<HVRHandGrabber>();
            if (hand != null && TavrInputHelper.GetPrimaryButtonDown(hand.HandSide))
            {
                // Нажата кнопка X (левая рука) или A (правая рука)
                TavrInputHelper.Vibrate(hand.HandSide, amplitude: 0.8f, duration: 0.15f);
            }
        }
    }
}
```

---

## 3. Ключевые сервисы SDK

### 3.1 `TavrLoadoutRegistry` (Мультимодовая совместимость)
Позволяет нескольким независимым модам сосуществовать без конфликтов Harmony-патчей:
- Автоматически создает кнопки в галерее арсенала.
- Расширяет массивы спавнеров игры с 15 до 16+ элементов.
- Перехватывает `Selection` и спавнит предмет нужного мода в выбранный карман.
- Свободные слоты остаются за стандартными гранатами или предметами других модов.

### 3.2 `TavrSocketHelper` (Разгрузка и карманы)
- `TavrSocketHelper.PrepareVestSocket(socket)`: настраивает HurricaneVR-сокет (отключает вырывание из рук `GrabsFromHand = false`, убирает кулдаун).
- `TavrSocketHelper.Holster(socket, item)`: корректно кладет предмет в подсумок игрока.

### 3.3 `TavrAssetHelper` (Меши и материалы)
- **Защита от выгрузки**: Unity при смене сцены очищает память (`UnloadUnusedAssets`). `TavrAssetHelper` автоматически устанавливает `HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave`.
- **Двухсторонние шейдеры**: автоматически настраивает URP/Lit с fallback на Standard и `_Cull = 0`.
- **Загрузка бинарных мешей**: `LoadMeshFromStream` парсит оптимизированный формат `.mesh`.

### 3.4 `TavrInputHelper` (VR Управление)
- `GetPrimaryButtonDown(hand)`: кнопка X (слева) / A (справа).
- `GetSecondaryButtonDown(hand)`: кнопка Y (слева) / B (справа).
- `GetTrigger(hand)`, `GetGrip(hand)`: аналоговые оси (0.0 – 1.0).
- `GetJoystick(hand)`: ось стика (Vector2).
- `Vibrate(hand, amplitude, duration, frequency)`: тактильный отклик (haptics).

---

## 4. Главные правила и подводные камни (Must-Know)

1. **Unity C++ Native Lifetime vs C# Reference**:
   *Никогда* не используйте `mesh ??= Load()` в статических полях для Unity-объектов! При смене сцены C++ объект уничтожается, а C# ссылка остается не null. Всегда проверяйте `if (mesh == null || !mesh)`.
2. **Физическая депенетрация (PhysX Depenetration)**:
   При создании связанных объектов (например, сброс гранаты с дрона или отсоединение магазина) всегда вызывайте `Physics.IgnoreCollision(colA, colB, true)`, иначе PhysX моментально выбросит импульс с силой в сотни ньютонов.
3. **Хват предметов из разгрузки**:
   Никогда не делайте сокеты жадными (`GrabsFromHand = true`). Игрок должен явно разжать руку в зоне сокета, чтобы положить предмет.
