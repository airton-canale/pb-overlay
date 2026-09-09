# PbOverlay

Desktop companion overlay for Point Blank. Reads the top-of-screen
portrait bar from the desktop framebuffer and displays a large, legible
alive-count (e.g. `4 v 2`) plus an FPS readout in a corner overlay.

## Anti-cheat posture

The app **never** touches the game process. No `OpenProcess`, no
`ReadProcessMemory`, no DLL injection, no D3D/OpenGL hook, no thread
manipulation, no writes into the game's install directory. Everything
comes from the desktop compositor and from OS-level ETW tracing.

The one thing the app queries about the game window is
`GetWindowThreadProcessId(hwnd, out pid)` — a pure OS lookup that opens
no handle — and the resulting PID is only ever passed as an argument to
the bundled PresentMon ETW consumer.

## Display modes

The click-through layered overlay composites over borderless-windowed
and windowed modes. Exclusive fullscreen may bypass DWM composition and
hide the overlay. **Run the game in borderless windowed for the overlay
to appear.**

## Layout

- `src/PbOverlay.Core` — capture, ROI, classification, FPS, config
  (no UI dependencies)
- `src/PbOverlay.App` — WPF app: main window (calibration + tuning +
  config reader) and the click-through overlay
- `tests/PbOverlay.Tests` — xUnit tests over `PbOverlay.Core`
- `tools/PresentMon` — bundled PresentMon CLI (see
  `THIRD-PARTY-NOTICES.md`)

## Build

Requires .NET 8 SDK on Windows 10 1903+ / Windows 11 x64.

```
dotnet build PbOverlay.sln
dotnet test  PbOverlay.sln
dotnet run   --project src/PbOverlay.App
```

## Config

Stored at `%APPDATA%\PbOverlay\config.json`. Schema is versioned; the
loader migrates forward and refuses to open a config with a newer
schema than the running binary understands.

ROI is stored as a **named profile list**, so multiple HUD layouts
(one per game mode) can be saved and switched between.

## FPS counter

Uses Intel PresentMon over ETW. Requires the app to be launched
elevated. When PresentMon is unavailable or the app is not elevated,
the FPS readout shows `n/a` and the alive-count overlay keeps working.
