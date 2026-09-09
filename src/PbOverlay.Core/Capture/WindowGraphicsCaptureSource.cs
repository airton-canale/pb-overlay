using Microsoft.Extensions.Logging;

namespace PbOverlay.Core.Capture;

/// <summary>
/// Windows.Graphics.Capture-backed source. WGC gives a per-window texture
/// (no cropping needed, no black bar issues on some drivers) and is the
/// preferred path.
///
/// ponytail: not yet implemented. This class exists so callers can code
/// against the preferred path today; when a case is found where
/// <see cref="DesktopDuplicationSource"/> is not enough, wire up
/// <c>GraphicsCaptureItemInterop</c> + <c>Direct3D11CaptureFramePool</c>
/// + <c>IDirect3DDxgiInterfaceAccess.GetInterface</c> here. The interface
/// contract is the same, so downstream code does not change.
/// </summary>
public sealed class WindowGraphicsCaptureSource : IFrameSource
{
    private readonly DesktopDuplicationSource _inner;

    public WindowGraphicsCaptureSource(
        ILogger<WindowGraphicsCaptureSource> _,
        ILogger<DesktopDuplicationSource> innerLog,
        int targetFps,
        Func<IntPtr?> hwndProvider,
        Func<IntPtr, bool> isForeground)
    {
        _inner = new DesktopDuplicationSource(innerLog, targetFps, hwndProvider, isForeground);
        _inner.FrameArrived += (s, e) => FrameArrived?.Invoke(this, e);
        _inner.TargetLost += (s, e) => TargetLost?.Invoke(this, e);
    }

    public event EventHandler<CapturedFrame>? FrameArrived;
    public event EventHandler? TargetLost;
    public bool IsRunning => _inner.IsRunning;
    public Task StartAsync(CancellationToken cancellationToken) => _inner.StartAsync(cancellationToken);
    public Task StopAsync() => _inner.StopAsync();
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
