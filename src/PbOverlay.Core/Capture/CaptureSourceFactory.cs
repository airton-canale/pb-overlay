using Microsoft.Extensions.Logging;

namespace PbOverlay.Core.Capture;

public interface ICaptureSourceFactory
{
    IFrameSource Create(int targetFps, Func<IntPtr?> hwndProvider, Func<IntPtr, bool> isForeground);
}

/// <summary>Tries WGC first; on failure the caller falls back to DXGI.</summary>
public sealed class CaptureSourceFactory : ICaptureSourceFactory
{
    private readonly ILoggerFactory _lf;
    public CaptureSourceFactory(ILoggerFactory lf) => _lf = lf;

    public IFrameSource Create(int targetFps, Func<IntPtr?> hwndProvider, Func<IntPtr, bool> isForeground)
    {
        // WGC is preferred, but the current implementation delegates to DXGI
        // until the WinRT path is wired up (see WindowGraphicsCaptureSource).
        return new WindowGraphicsCaptureSource(
            _lf.CreateLogger<WindowGraphicsCaptureSource>(),
            _lf.CreateLogger<DesktopDuplicationSource>(),
            targetFps, hwndProvider, isForeground);
    }
}
