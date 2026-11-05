# Project: Tactical Assault FPV

## Architecture
Tactical Assault FPV is a MelonLoader v0.7.3 / Unity 6 / Il2Cpp mod for Tactical Assault VR.
The architecture consists of:
1. Core Drone:
   - `DronePhysics.cs`: 6-DoF flight dynamics, attitude PIDs, hover hold, propeller spinning.
   - `DroneExplosive.cs`: Proximity/impact arming, fuse logic, spherical blast damage, VFX/SFX spawning.
   - `DroneCameraRig.cs`: FPV nose camera, wide-angle lens, camera tilt servo, target RenderTexture.
   - `DroneMotorAudio.cs`: Procedural audio generation of 4-motor brushless ESC PWM and prop chop.
2. VR Interaction & Hardware:
   - `DroneFactory.cs`: Assembly of drone physical hull, Hurricane VR (HVRGrabbable, HVRPosableGrabPoint) grip points, material assignment.
   - `RCTablet.cs`: Operator handheld controller with 16:10 FPV monitor, Betaflight OSD overlay, drone switching, motor/warhead arming, locomotion suppression.
   - `FPVLoadoutIntegration.cs`: Armory vest integration (3 drone slots, 1 tablet slot), equip button hooks, socket filters, scene transitions.
3. Mod Lifecycle & Interop:
   - `ModMain.cs`: MelonMod entry point, Il2Cpp type registration via ClassInjector, Harmony prefixes/postfixes on player locomotion, weapon select, and health.
   - `TacticalAssaultFPV.csproj`: Target net6.0, Il2Cpp references, post-build automated deployment.
4. Diagnostics & Testing:
   - `Diagnostics.cs`: Self-test runner verifying subsystem instantiation, audio synthesis, multi-drone cycling, and component integrity.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Flight Physics Grabbing Guard | Inhibit drone thrust/torque while grabbed by hand or socketed in vest | M1 | explorer_survey_1 |
| 2 | Explosive Target Deduplication | Deduplicate EnemyHealth/PlayerHealth targets in blast radius to prevent 6x multi-collider damage | M1 | explorer_survey_1 |
| 3 | Procedural Motor Audio Longevity | Add HideAndDontSave and safe null check to procedural AudioClip across scene reloads | M1 | explorer_survey_1 |
| 4 | Motor Audio Loop Seamlessness | Fix phase wrap calculation at crossfade boundary to eliminate audible loop clicks | M1 | explorer_survey_1 |
| 5 | Camera Rig Aspect Ratio | Match RenderTexture aspect ratio (16:10, e.g. 512x320) and camera aspect to tablet screen quad | M1 | explorer_survey_1 |
| 6 | Flight Axes Orthogonality | Ensure forwardXZ and rightXZ vectors in DronePhysics are strictly orthogonal | M1 | explorer_survey_1 |
| 7 | Detonation Audio Caching | Cache explosion audio clip references instead of synchronous Resources.FindObjectsOfTypeAll on impact | M1 | explorer_survey_1 |
| 8 | Event Accessor Il2Cpp Hiding | Add [method: HideFromIl2Cpp] to DroneExplosive.OnDetonated event accessors | M1 | explorer_survey_3 |
| 9 | Drone Cycle Switching Fix | Fix SwitchToNextDrone early exit condition so operator can connect to sole surviving drone | M2 | explorer_survey_2 |
| 10 | Locomotion Freeze Reset | Ensure RCTablet.ResetPlayerFrozen() is called on player respawn in PlayerHealth.Start | M2 | explorer_survey_2 |
| 11 | Armory Vest Equip Drone Sync | Call SyncTabletDrones() inside OnEquipButtonPressed so tablet references vest drones | M2 | explorer_survey_2 |
| 12 | Drone Collision Hull & Grip | Replace sledgehammer collider with dedicated BoxCollider and align HVRPosableGrabPoint | M2 | explorer_survey_2 |
| 13 | Material Instance Leak Prevention | Cache shared materials in DroneFactory.SetColor to eliminate native C++ memory leaks | M2 | explorer_survey_2 |
| 14 | Per-Drone Battery Telemetry | Decouple battery voltage from static/shared tablet state to per-drone tracking | M2 | explorer_survey_2 |
| 15 | VR OSD Allocation Optimization | Eliminate per-frame string concatenation and LINQ/lambda allocations in UpdateOSD | M2 | explorer_survey_2 |
| 16 | Vest Socket Filter Safety | Preserve native vest socket tags and filters during loadout attachment | M2 | explorer_survey_2 |
| 17 | Multiplayer Local Player Guards | Restrict PlayerHealth and Spawner patches to local player (photonView.IsMine, !isDummyHealth) | M3 | explorer_survey_3 |
| 18 | Harmony Prefix Null Safety | Add defensive null guards (if (__instance == null) return true;) across all 11 Harmony prefix hooks | M3 | explorer_survey_3 |
| 19 | Scene Lifecycle & Emergency Keys | Implement OnSceneWasLoaded cleanup and OnUpdate F8 emergency spawn / F9 diagnostics trigger | M3 | explorer_survey_3 |
| 20 | Scene-Scoped Armory Terminal | Restrict SetupArmoryTerminal to main menu / armory scenes to avoid cluttering combat maps | M3 | explorer_survey_3 |
| 21 | Automated PostBuild Deployment | Add MSBuild PostBuild target to copy TacticalAssaultFPV.dll to game Mods directory | M3 | explorer_survey_3 |
| 22 | Comprehensive Diagnostic Suite | Expand Diagnostics.cs to validate all 9 subsystems with pass/fail reporting | M4 | explorer_survey_3 |
| 23 | Clean Build & Runtime Verification | Compile with 0 errors and verify diagnostics run cleanly in deployment | M4 | ORIGINAL_REQUEST |
| 24 | Drone HVR Collider & Socket Fix | Remove shoulder tags, re-bind HVRGrabbable colliders via SetupColliders to allow vest socketing & VR hand grab | M5 | USER_REPORT |
| 25 | URP FPV Stream Binding & Screen | Bind _BaseMap/_MainTex/_BaseColor on unlit screen quad with Cull Off to fix black camera feeds | M5 | USER_REPORT |
| 26 | Visual Explosion VFX & Point Light | Add dynamic point light blast flash and expanding fireball shockwave to DroneExplosive | M5 | USER_REPORT |
| 27 | OSD Monospace & Right-Stick Debounce | Debounce right-stick clicks to eliminate camera flickering; add backface culling to TextMesh | M5 | USER_REPORT |
| 28 | Trigger Power-On & ESC Startup Chime | Hold drone in hand + squeeze trigger to power on; plays ESC chime, lights neon LED, links to tablet | M6 | USER_REPORT |
| 29 | Spawner Debounce & Deduplication | Prevent duplicate spawner invocations creating phantom drones; ensure stable tablet pairing | M6 | USER_REPORT |
| 30 | Vest Grenade Pouch Return & Socketing | Attach HVRSocketable to Drone & Tablet; auto-unfreeze player and auto-power-off/disarm on pouch return | M7 | USER_REPORT |
| 31 | Warm 3D Spatial Acoustic Model | Low-frequency prop hum & chop (360/720Hz), 100% 3D spatialized audio, eliminate harsh 7kHz carrier scream | M7 | USER_REPORT |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Core Drone Subsystems Remediation | Fix flight physics grab guard, explosive damage deduplication, audio clip caching & crossfade, 16:10 camera rig aspect, and Il2Cpp event attributes in DronePhysics, DroneExplosive, DroneCameraRig, DroneMotorAudio | none | DONE (Verified: 0 errors, 44 stress tests pass, CLEAN audit) |
| M2 | VR Interaction, Factory & Loadout Remediation | Fix drone switching deadlock, respawn freeze reset, equip sync, collision hull & poser grip, material caching, battery telemetry, and OSD allocations in DroneFactory, RCTablet, FPVLoadoutIntegration | M1 | DONE (Verified: 0 errors, 93 stress tests pass, CLEAN audit) |
| M3 | Lifecycle Mod Entry, Harmony Safety & Build Deployment | Add multiplayer guards, Harmony prefix null checks, scene cleanup, F8/F9 hotkeys in ModMain, and PostBuild deployment in TacticalAssaultFPV.csproj | M2 | DONE (Verified: 0 errors, 148 stress tests pass, CLEAN audit, auto-deployed) |
| M4 | Diagnostics Suite Expansion & Final Verification | Expand Diagnostics.cs to test all 9 subsystems, eliminate dummy asserts, run build, verify deployment DLL, and validate diagnostics pass | M1, M2, M3 | DONE (Verified: 0 errors, 182 stress tests pass, 0 dummy asserts, CLEAN audit, auto-deployed) |
| M5 | Physical Grabbing, Camera Feed & Visual VFX Hotfix | Eliminate socket tag rejection, re-register grabbable colliders, bind URP BaseMap texture on screen, debounce right-stick switching, add visual explosion VFX and pilot audio | M4 | DONE (Verified: 0 errors, 182 stress tests pass, deployed) |
| M6 | Trigger Power-On System & Phantom Drone Fix | Require trigger press in hand to power on drone, procedural ESC chimes, status LED, dynamic tablet link, eliminate duplicate spawner instantiation | M5 | DONE (Verified: 0 errors, 182 stress tests pass, deployed) |
| M7 | Vest Pouch Return & Warm 3D Acoustic Redesign | Enable full return to vest pouches, auto-power-off on stow, auto player locomotion unfreeze, deep pleasant propwash sound, 100% 3D spatial sound | M6 | DONE (Verified: 0 errors, 182 stress tests pass, deployed) |

## Interface Contracts
### DronePhysics ↔ DroneFactory / VR Grab
- `DronePhysics` checks `HVRGrabbable.IsHandGrabbed` and `HVRGrabbable.IsSocketed` or exposes `SetGrabbed(bool)` to suspend flight forces in `FixedUpdate()`.
### DroneExplosive ↔ RCTablet
- `DroneExplosive.HasDetonated` property remains public boolean; `OnDetonated` event accessors annotated with `[method: HideFromIl2Cpp]`.
### RCTablet ↔ Player Locomotion / ModMain
- `RCTablet.IsPlayerFrozen` readable by Harmony patches; `RCTablet.ResetPlayerFrozen()` callable from `PlayerHealth_Start_Patch` or scene load.
### FPVLoadoutIntegration ↔ RCTablet
- `FPVLoadoutIntegration.SyncTabletDrones()` registers active drones to `RCTablet.RegisterDrone(GameObject)`.

## Code Layout
- `src/TacticalAssaultFPV/Drone/DronePhysics.cs` (M1 - DONE)
- `src/TacticalAssaultFPV/Drone/DroneExplosive.cs` (M1 - DONE)
- `src/TacticalAssaultFPV/Drone/DroneCameraRig.cs` (M1 - DONE)
- `src/TacticalAssaultFPV/Drone/DroneMotorAudio.cs` (M1 - DONE)
- `src/TacticalAssaultFPV/Drone/DroneFactory.cs` (M2 - DONE)
- `src/TacticalAssaultFPV/Controller/RCTablet.cs` (M2 - DONE)
- `src/TacticalAssaultFPV/Loadout/FPVLoadoutIntegration.cs` (M2 - DONE)
- `src/TacticalAssaultFPV/ModMain.cs` (M3 - DONE)
- `src/TacticalAssaultFPV/TacticalAssaultFPV.csproj` (M3 - DONE)
- `src/TacticalAssaultFPV/Diagnostics.cs` (M4 - DONE)
