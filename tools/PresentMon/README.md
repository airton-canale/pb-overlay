# Bundled PresentMon

Drop the pinned PresentMon binary here as `PresentMon.exe`, alongside
`LICENSE.txt` (copied verbatim from the PresentMon repo) and
`VERSION.txt` (containing only the version string, e.g. `2.2.0`).

`PbOverlay.App` copies the contents of this directory to the build
output under `tools/PresentMon/` via the `<Content Include="...">`
entry in `PbOverlay.App.csproj`.

The FPS module (`PbOverlay.Core/Fps/PresentMonProcessSource.cs`)
resolves the binary as
`Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon", "PresentMon.exe")`.
If the file is missing, the FPS module falls back to
`NullFrameTimingSource` and the UI shows `n/a`.
