using Microsoft.Extensions.Logging;

namespace PbOverlay.Core.Capture;

public interface ICaptureSourceFactory
{
    IFrameSource Create(int targetFps, Func<IntPtr?> hwndProvider, Func<IntPtr, bool> isForeground);
}

public sealed class CaptureSourceFactory : ICaptureSourceFactory
{
    private readonly ILoggerFactory _lf;
    public CaptureSourceFactory(ILoggerFactory lf) => _lf = lf;

    public IFrameSource Create(int targetFps, Func<IntPtr?> hwndProvider, Func<IntPtr, bool> isForeground) =>
        new DesktopDuplicationSource(
            _lf.CreateLogger<DesktopDuplicationSource>(),
            targetFps, hwndProvider, isForeground);
}
