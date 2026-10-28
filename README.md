# G-Helper: ROG Strix G513RM Edition

A specialized, debloated fork of [G-Helper](https://github.com/seerge/g-helper) custom-tailored specifically for the **ASUS ROG Strix G513RM** laptop.

```
Hardware Target:
Model:   ASUSTeK COMPUTER INC. ROG Strix G513RM (BIOS G513RM.327)
CPU:     AMD Ryzen 7 6800H (8 Cores / 16 Threads, Zen 3+ Rembrandt)
GPU:     NVIDIA GeForce RTX 3060 Laptop GPU (140W max TGP: 115W base + 25W Dynamic Boost)
iGPU:    AMD Radeon 680M Graphics (RDNA 2) with MUX Switch + Advanced Optimus
Memory:  32 GB DDR5 RAM (Dual-Channel DDR5-4800)
Display: 15.6" TL156VDXP0101 Panel + External ASUS VA259HGA Monitor
Aura:    4-Zone RGB Keyboard + Front/Side Flipped 6-LED Underglow Lightbar (USB 0x19B6)
```

---

## Key Custom Features

### 1. Live G513RM Hardware Telemetry Header
A 3-card live telemetry banner docked at the top of the window:
* **Ryzen 7 6800H (`buttonTelemCPU`)**: Real-time package temperature, PawnIO `RyzenSMU` package power consumption (`18W` idle up to `70W` sustained), and CPU fan speed in RPM. Click to open CPU Fan and Power curves.
* **RTX 3060 (`buttonTelemGPU`)**: Real-time NVIDIA NVML core temperature, board wattage (`23W` to `140W`), and GPU fan speed in RPM, or `Eco / 0W` when powered down. Click to open GPU Clock and Boost settings.
* **32GB DDR5 RAM + 1-Click Purge (`buttonTelemRAM`)**: Real-time memory load via `GlobalMemoryStatusEx` (`16.5 / 31 GB (52%)`). Clicking triggers `EmptyWorkingSet` across user processes and `NtSetSystemInformation` (`MemoryEmptyWorkingSets` + `MemoryPurgeStandbyList`) to free standby memory before loading games or heavy creative software.

### 2. Four Calibrated G513RM Presets & Fan Curves
Calibrated specifically for Rembrandt 6800H power limits and GA106 RTX 3060 clock curves:

| Preset | Base Mode | CPU Limits (SPL / sPPT / fPPT) | CPU Temp Cap | RTX 3060 Dynamic Boost | RTX 3060 Clocks | Thermal Target & Acoustic Profile |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Silent** (`2`) | Silent (`2`) | 25W / 35W / 45W | 75°C | +5W (120W max) | Stock (+0 / +0 MHz) | True Zero-RPM idle below 50°C, whisper quiet |
| **Balanced** (`0`) | Balanced (`0`) | 45W / 65W / 80W | 85°C | +15W (130W max) | +50 Core / +100 Memory | Balanced daily linear fan ramp |
| **Sweet Spot** (`3`)| Turbo (`1`) | 40W / 55W / 75W | 83°C | +25W (140W full TGP) | +120 Core / +250 Memory | Maximum GPU performance with lower CPU thermals |
| **Turbo** (`1`) | Turbo (`1`) | 70W / 90W / 115W | 92°C | +25W (140W full TGP) | +100 Core / +200 Memory | High-airflow aggressive curve for benchmarks |

### 3. Smart Game Profile Switcher (`ProcessProfileWatcher`)
* Built-in background watcher with toggle (`Auto Game: ON / OFF`) right on the Main Window.
* Automatically switches to `Sweet Spot` mode when an active game or sustained 3D workload (>45W dGPU draw) is detected.
* Automatically restores your previous performance mode once the game exits.

### 4. G513RM 4-Zone Keyboard + Flipped Lightbar Controls
* Hardcoded for G513RM USB controller `0x19B6` (`IsBacklightZones() => true`, `IsStrix4ZoneFlipped() => true`).
* Four direct zone color pickers (`Z1 Left`, `Z2 Mid-L`, `Z3 Mid-R`, `Z4 + Bar`) on the main interface to quickly customize per-zone RGB colors and the 6-LED underglow bar.

### 5. Strict Manual Display & External Monitor Safe Mode
* Automatically bypasses refresh rate switching on unplug when an external monitor (`ASUS VA259HGA`) is connected.
* Never forces GPU mode changes during lid events.
* Dedicated `60%`, `80%`, and `100%` quick-cap buttons for the battery charge limit.

### 6. Live ASUS Services Blocker
* Dedicated 1-click status badge on the Main Window (`ASUS Services: X [Stop]`).
* Disables or re-enables all background ASUS bloat services (`ArmouryCrateControlInterface`, `ASUSOptimization`, `AsusAppService`, `LightingService`, etc.) on the fly.

---

## Hardware Debloating (Zero Overhead)

All non-G513RM hardware drivers, controllers, and redundant assets were stripped from this build:
* **Removed hardware modules**: ROG Ally, AniMe Matrix, Slash Lighting, XG Mobile, Keystone, ASUS external peripherals (mice, keyboards, headsets), Intel MSR, and Intel VRAM.
* **Stripped localizations**: 22 unused `.resx` files removed to produce a lean, fast English single-file executable.
* **Hardcoded model capabilities**: `AppConfig.cs` hardcodes `IsStrix() => true`, `IsROG() => true`, `IsASUS() => true`, and returns `false` for all non-G513RM device checks without redundant runtime queries.

---

## Building from Source

Requirements:
* Windows 11 / 10 64-bit
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download)

Publish a single-file executable:
```powershell
dotnet publish app/GHelper.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```
The output executable will be generated at `dist/GHelper-G513RM.exe`.

---

## Upstream & Credits

Forked from the excellent [seerge/g-helper](https://github.com/seerge/g-helper) by Serge.
Modified and optimized by [Christian Ikajaya (DaLonelyCat)](https://github.com/DaLonelyCat).
Licensed under the [MIT License](LICENSE).
