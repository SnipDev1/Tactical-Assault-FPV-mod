# Tactical Assault FPV - Strike Drone Mod

[![CI Test Suite](https://github.com/dmitry/tactical-assault-fpv/actions/workflows/ci.yml/badge.svg)](https://github.com/dmitry/tactical-assault-fpv/actions/workflows/ci.yml)
[![Platform: Windows | Linux](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%28Steam%20Deck%29-0078D6)](https://store.steampowered.com/app/2314440/Tactical_Assault_VR/)
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%208.0-512BD4)](https://dotnet.microsoft.com/)
[![MelonLoader](https://img.shields.io/badge/MelonLoader-0.6.x-brightgreen)](https://melonwiki.xyz/)
[![Target Game](https://img.shields.io/badge/Game-Tactical%20Assault%20VR-blue)](https://store.steampowered.com/app/2314440/Tactical_Assault_VR/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Language](https://img.shields.io/badge/Language-RU%20%7C%20EN-orange)](#russian-documentation--документация-на-русском)

[**Русская версия документации (Russian README)**](README_RU.md)

A comprehensive, physically simulated **FPV Kamikaze Strike Drone Mod** for **Tactical Assault VR** (Unity 6 / IL2CPP x64). Fully compatible with **Windows 10/11** and **Linux / SteamOS (Proton / WiVRn)**. Seamlessly integrated into the game's armory, VR inventory sockets, and flight mechanics.

---

## Key Features

* **3-Drone Strike Fleet**: Carry up to three strike drones in your grenade/utility pouches simultaneously.
* **Cyclic Drone Switching**: Switch active drone video feeds and flight controls cyclically (`Right Stick Click`) with per-drone battery tracking and dead drone auto-skipping.
* **Tactical RC Tablet with Betaflight OSD**: Real-time 512x320 video feed, central target reticle `[ + ]`, flight telemetry (altitude, ground speed, battery voltage, camera tilt, and armed/safe status banners).
* **Continuous Smooth Optical Zoom (1.0x to 10.0x)**: Pure continuous zoom with zero stepped artifacts, allowing seamless target magnification from wide-angle situational awareness (115° FOV) down to high-precision telephoto reconnaissance (11.5° FOV).
* **90° Downward Camera Tilt (Nadir View)**: Full analog camera tilt range from +75° up to -90° (looking straight down perpendicular to the ground) for top-down urban reconnaissance and steep diving strikes.
* **Smooth Continuous Altitude Hold**: Velocity-integrated altitude hold (`_holdAltitude += climbInput * climbSpeed * dt`) eliminating stepped altitude transitions.
* **Authentic 7-9" FPV Drone Chassis**: Scaled compact airframe (0.8x) with elongated aggressive propeller blades and an authentic cylindrical impact warhead.
* **Procedural 4-Harmonic Acoustic Synthesizer**: Click-free procedural audio engine synthesizing brushless ESC carrier whine (2640 & 3300 Hz) and 3-blade prop wash (720 & 1440 Hz) with C1 continuous smoothstep loop wrapping.
* **Trigger Power-On & Safety Arming**: Drones require a trigger pull when grabbed to power on and register with the tablet. Impact detonation is strictly inhibited while held in hand or stowed in vest pouches.
* **Tuned Explosive Blast Impulse**: Lethal shrapnel and blast damage with natural knockdown physics that prevent unrealistic ragdoll catapulting.
* **Smart Proximity Holster Assist**: Magnetic snap assistance for stowing tablet and drones into vest utility and grenade pouches without accidental drops.
* **Preserved Weapon Holsters**: Strict socket isolation prevents accidental placement of primary and secondary firearms into grenade/utility slots.

---

## VR Controls & Keybindings

### Left Controller (Flight & Warhead)

| Input | Action |
| :--- | :--- |
| **Thumbstick Y** | Altitude / Vertical Flight (Climb / Descent) |
| **Thumbstick X** | Yaw (Heading Rotation) |
| **Button X (Primary)** | Toggle Flight Control / Resume Flight / Disarm Motors |
| **Button Y (Secondary)** | Toggle Warhead Arming (`[ ! WARHEAD ARMED ! ]` / Safe) |
| **Left Trigger (LT)** | Tilt Camera Down (down to `-90°` nadir / straight down) |

### Right Controller (Flight, Camera & Optics)

| Input | Action |
| :--- | :--- |
| **Thumbstick Y** | Pitch (Forward / Backward Flight) |
| **Thumbstick X** | Roll (Lateral Bank / Strafe) |
| **Thumbstick Click** | Switch Active Drone (`1` $\to$ `2` $\to$ `3` $\to$ `1`) |
| **Button A (Hold)** | Smooth Optical Zoom In (up to `10.0X`) |
| **Button B (Hold)** | Smooth Optical Zoom Out (down to `1.0X`) |
| **Right Trigger (RT)** | Tilt Camera Up (up to `+75°` skyward) |

### Physical Hand Interactions

* **Take / Stow**: Grab tablet or drones from chest/vest pouches with grip. Move close to pouch to holster.
* **Drone Power-On**: When holding a drone from the pouch, pull the trigger once — the LED will illuminate and the drone registers on the RC tablet.
* **Emergency Spawn (Keyboard)**: Press `F8` on keyboard to spawn an emergency tablet and drone directly in front of the player.

---

## Installation

### Prerequisites

* **Tactical Assault VR** installed via Steam.
* **MelonLoader (v0.6.x or newer)** for Unity 6 / IL2CPP x64 games:
  * Windows / Linux Installer: [MelonLoader Releases](https://github.com/LavaGang/MelonLoader.Installer/releases).

---

### Windows Installation

#### Automated (1-Click)
1. Double-click `install.bat` (or run `.\install.ps1` in PowerShell).
2. The script will automatically locate your Steam installation (`C:\`, `D:\`, or `E:\` drive), compile the mod, and deploy `TacticalAssaultFPV.dll` into `Tactical Assault VR\Mods\`.

#### Manual
1. Download `TacticalAssaultFPV.dll` from the latest [Releases](https://github.com/dmitry/tactical-assault-fpv/releases).
2. Copy `TacticalAssaultFPV.dll` into:
   ```
   C:\Program Files (x86)\Steam\steamapps\common\Tactical Assault VR\Mods\
   ```
3. Start the game normally via Steam in VR!

---

### Linux / Steam Deck Installation

#### Automated
Run the installation script in terminal:
```bash
./install.sh "/path/to/Tactical Assault VR"
```
*(If omitted, defaults to standard Steam library location)*.

#### Linux / Proton / WiVRn Launch Options
In your Steam Library, right-click **Tactical Assault VR** $\to$ **Properties** $\to$ **Launch Options**:
```bash
WINEDLLOVERRIDES="version=n,b" %command%
```
*(If running under WiVRn Flatpak, prepend: `PRESSURE_VESSEL_IMPORT_OPENXR_1_RUNTIMES=1 PRESSURE_VESSEL_FILESYSTEMS_RW=/var/lib/flatpak/app/io.github.wivrn.wivrn`)*.

---

## Building from Source

### Requirements

* [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) (for compiling the mod)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for the standalone stress test suite)

### Build Commands

```bash
# Windows (PowerShell / Command Prompt)
dotnet build src\TacticalAssaultFPV\TacticalAssaultFPV.csproj -c Release

# Linux / macOS (Bash)
dotnet build src/TacticalAssaultFPV/TacticalAssaultFPV.csproj -c Release

# Run the 219 empirical stress tests (Cross-Platform)
dotnet run --project tests/TacticalAssaultFPV.StressTests/TacticalAssaultFPV.StressTests.csproj -c Release
```

---

## Architecture & Code Structure

```
├── src/
│   └── TacticalAssaultFPV/
│       ├── Config/ModConfig.cs               # Runtime config (speeds, resolutions, FOV)
│       ├── Controller/RCTablet.cs           # RC tablet logic, OSD rendering, VR input router
│       ├── Drone/
│       │   ├── DroneCameraRig.cs            # FPV camera, 10x smooth zoom, 90° tilt
│       │   ├── DroneExplosive.cs            # Impact detonation, shrapnel raycast, safety locks
│       │   ├── DroneFactory.cs              # 0.8x compact chassis, props, materials & colliders
│       │   ├── DroneMotorAudio.cs           # Procedural 4-harmonic acoustic engine
│       │   └── DronePhysics.cs              # Smooth continuous altitude hold & PD flight
│       ├── Loadout/
│       │   ├── ArmoryEquipButtonTrigger.cs  # Physical 3D button in Armory room
│       │   └── FPVLoadoutIntegration.cs     # Socket filters, 3-drone sync & vest holsters
│       ├── Diagnostics.cs                   # In-game runtime self-test diagnostic suite
│       └── ModMain.cs                       # MelonLoader lifecycle & scene guards
├── tests/
│   ├── TacticalAssaultFPV.StressTests/      # Standalone 219-test empirical verification suite
│   ├── ChallengerM4/                        # Adversarial memory leak & exception stress tests
│   └── ChallengerM4R2/                      # Monte Carlo flight dynamics & permutation tests
├── install.bat / install.ps1                # Windows automated deployment scripts
├── install.sh                               # Linux automated deployment script
├── uninstall.bat / uninstall.ps1            # Windows rollback scripts
├── uninstall.sh                             # Linux rollback script
└── LICENSE                                  # MIT License
```

---

## Testing & Quality Assurance

The codebase includes an empirical stress test suite continuously verified on both **Windows** and **Ubuntu** via GitHub Actions:
- **219 automated unit & regression tests** covering flight math, quaternion orientation, continuous altitude hold, optical zoom, aspect ratios, and boundary conditions.
- **500,000 Monte Carlo iterations** validating multi-drone switching permutations and zero flakiness.
- **Zero dynamic memory leaks** during repetitive scene transitions and drone detonations.

---

## License

This project is licensed under the [MIT License](LICENSE).
Tactical Assault VR is a trademark of its respective owner. This modification is an unofficial fan creation.
