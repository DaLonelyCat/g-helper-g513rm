# DESIGN.md: G-Helper ROG Strix G513RM Edition

## 1. Identity & Character
- **Product**: G-Helper: ROG Strix G513RM Edition (`GHelper-G513RM.exe`)
- **Target Hardware**: 2022 ASUS ROG Strix G15 (`G513RM`, BIOS `G513RM.327`, AMD Ryzen 7 6800H, NVIDIA GeForce RTX 3060 140W Laptop GPU, 32GB DDR5 RAM, 4-Zone Flipped RGB Keyboard + Lightbar)
- **Visual Archetype**: Industrial Hardware Telemetry & Control Deck. Built for immediate hardware situational awareness, zero animation lag, and unambiguous physical state feedback.
- **Personality**: Precision-engineered, compact, tactile, high-contrast, zero bloat.

## 2. Liveliness Dials (antislop Part 3)
- **ENERGY**: `2` (Focused semantic hardware state highlights for active performance profiles and thermal/service states)
- **RHYTHM**: `2` (Structured 3-card live hardware telemetry deck at top, followed by modular hardware control sections for Performance, GPU/Display, 4-Zone Aura, and Battery/Services)
- **MOTION**: `1` (Instant native WinForms state transitions; zero decorative bounce or fade delays)

## 3. Color System & WCAG AA Contrast Ratios (antislop R-25)

### Dark Theme (Default: Strix Carbon)
- **Window Canvas (`formBack`)**: `#121417` (`RGB(18, 20, 23)`)
- **Card / Panel Surface (`buttonMain`)**: `#1C2026` (`RGB(28, 32, 38)`)
- **Elevated / Secondary Control (`buttonSecond`)**: `#262C35` (`RGB(38, 44, 53)`)
- **Subtle Structural Border (`borderMain`)**: `#2E3642` (`RGB(46, 54, 66)`)
- **Primary Text (`foreMain`)**: `#F0F4F8` (`RGB(240, 244, 248)`) -> Contrast vs `#121417`: **15.4:1** (WCAG AAA)
- **Secondary / Telemetry Sub-label (`colorGray`)**: `#9BA6B2` (`RGB(155, 166, 178)`) -> Contrast vs `#121417`: **6.8:1**; Contrast vs `#1C2026`: **5.7:1** (Exceeds WCAG AA 4.5:1)

### Light Theme (High-Visibility Daylight)
- **Window Canvas (`formBack`)**: `#F4F6F8` (`RGB(244, 246, 248)`)
- **Card / Panel Surface (`buttonMain`)**: `#FFFFFF` (`RGB(255, 255, 255)`)
- **Elevated / Secondary Control (`buttonSecond`)**: `#E6EBF0` (`RGB(230, 235, 240)`)
- **Subtle Structural Border (`borderMain`)**: `#CBD4DF` (`RGB(203, 212, 223)`)
- **Primary Text (`foreMain`)**: `#11161C` (`RGB(17, 22, 28)`) -> Contrast vs `#FFFFFF`: **18.1:1** (WCAG AAA)
- **Secondary / Telemetry Sub-label (`colorGray`)**: `#4A5562` (`RGB(74, 85, 98)`) -> Contrast vs `#FFFFFF`: **7.5:1** (Exceeds WCAG AA 4.5:1)

### Semantic Hardware Profile Accents
- **Silent (`colorEco` / Mode 2)**: `#06B48A` (`RGB(6, 180, 138)`) -> Quiet efficiency / Eco indicator
- **Balanced (`colorStandard` / Mode 0)**: `#3AAEEF` (`RGB(58, 174, 239)`) -> Daily 45W balanced operation
- **Turbo (`colorTurbo` / Mode 1)**: `#FF3B30` (`RGB(255, 59, 48)`) -> Full 140W RTX 3060 + 70W CPU power
- **Sweet Spot (`colorCustom` / Mode 3)**: `#F59E0B` (`RGB(245, 158, 11)`) -> Calibrated 40W 6800H + 140W RTX 3060 gaming sweet spot

## 4. Typography (antislop R-06)
- **UI Controls & Section Headers**: `Segoe UI` / `Segoe UI Semibold` (native Windows legibility at 100%-150% DPI scaling)
- **Live Telemetry Readouts**: `Consolas` / `Segoe UI Semibold` tabular numeric alignment so live changes in `°C`, `W`, `RPM`, and `MB` never jitter horizontally.

## 5. Required UI States (antislop R-27)
Every telemetry card and hardware control implements explicit **Loading**, **Empty / Sleeping**, and **Active / Error** states:
1. **CPU Telemetry (`Ryzen 7 6800H`)**:
   - *Loading*: `6800H: Polling sensors...`
   - *Active*: `6800H: 52°C | 18.4W | 2100 RPM`
   - *Fallback (No SMU)*: `6800H: 52°C | ACPI Mode | 2100 RPM`
2. **GPU Telemetry (`RTX 3060`)**:
   - *Sleeping / Eco Mode*: `RTX 3060: Eco / Off (0W) | 0 RPM`
   - *Active*: `RTX 3060: 64°C | 118W | 3420/6144MB | 3200 RPM`
   - *Driver Unavailable*: `RTX 3060: NvAPI Offline`
3. **Memory Telemetry (`32GB DDR5`)**:
   - *Active*: `RAM: 11.4 / 32.0 GB (36%)` + `[Purge RAM]` button
   - *Action Feedback*: Button displays `Freed 1,240 MB` for 3 seconds after purge, or `Admin Needed` if privileges are insufficient.
4. **ASUS Service Blocker**:
   - *Running*: `ASUS Services: 11 Active [Stop All]` (Amber warning accent)
   - *Stopped*: `ASUS Services: 0 Active [Start]` (Teal clean accent)
