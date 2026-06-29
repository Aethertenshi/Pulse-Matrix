# Pulse Matrix ⚡

> A high-velocity 2D rhythm-action game powered by `.osu` beatmaps, dynamic track geometry, and real-time audio visualization.

![C#](https://img.shields.io/badge/Language-C%23-blue)
![Engine](https://img.shields.io/badge/Engine-Rei2D%20%2F%20SDL3-purple)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)

---

## 🌟 Overview

**Pulse Matrix** transforms traditional rhythm beatmaps into an interactive 2D track-hopping experience. Players control a glowing diamond gem navigating across rhythmically generated waypoints, executing directional taps and golden slider holds in sync with song tempos.

---

## ✨ Key Features

### 🎵 Native `.osu` Beatmap Integration
* **Instant Song Support**: Drag-and-drop `.osz` packages directly into the window to load songs instantly.
* **Polyphonic Hitsounds**: Built-in 8-track audio buffer pool for zero-latency hitsound feedback.

### 🕹️ Dynamic Rhythm Mechanics
* **Directional Track Hopping**: Navigate rhythm circuits using `WASD` or `Arrow Keys`.
* **Golden Hold Sliders**: Hold required directional keys to slide smoothly along golden trails. Features dynamic Slider Velocity (SV) scaling and support for oscillating repeat loops (`↩`).
* **Linear Approach Rings**: Crisp $3.0\times \to 1.0\times$ linear contraction window for reliable visual timing reads.

### 🛣️ Flow Momentum Filtering
* Algorithmic directional smoothing prevents immediate $180^\circ$ u-turns during fast note streams, transforming jittery zig-zags into graceful curves and rhythm circuits.

### 🎨 Audio-Reactive Visuals
* **Deluxe Glassmorphic Dark UI**: High-contrast modern dark-mode aesthetic with customizable accent palettes.
* **FFT Spectrum Underglow**: Real-time BASS audio spectrum visualizers with reactive beat-pulsing atmospheres.

---

## 🎮 Controls

| Action | Primary Key | Secondary Key |
| :--- | :---: | :---: |
| **Move / Hit Direction** | `W` `A` `S` `D` | `↑` `←` `↓` `→` |
| **Hold Sliders** | Hold Direction Key | Hold Direction Key |
| **Pause / Menu** | `Escape` | `Escape` |
| **Install Song** | Drag & Drop `.osz` File onto Window |

---

## 🛠️ Getting Started

### Prerequisites
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
* Windows OS

### Building & Running
1. Clone the repository:
   ```bash
   git clone https://github.com/your-username/Pulse-Matrix.git
   cd Pulse-Matrix
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Launch the game:
   ```bash
   dotnet run
   ```

---

## 📄 License

This project is developed for educational and personal rhythm gaming enjoyment. All beatmaps and music assets belong to their respective creators.
