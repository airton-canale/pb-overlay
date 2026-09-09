using Microsoft.Extensions.Logging;
using PbOverlay.App.Overlay;
using PbOverlay.Core.Capture;
using PbOverlay.Core.Config;
using PbOverlay.Core.Fps;

namespace PbOverlay.App;

/// <summary>
/// Process-wide service locator. Deliberately a static bag rather than a DI
/// container — the WPF shell only has a handful of singletons and each tab
/// grabs what it needs directly. If this grows past ~10 fields, promote to
/// a proper container.
/// </summary>
// ponytail: static bag over DI container; swap to Microsoft.Extensions.DependencyInjection if scope creeps.
internal static class AppServices
{
    public static ILoggerFactory LoggerFactory { get; set; } = null!;
    public static AppConfig Config { get; set; } = null!;
    public static ICaptureSourceFactory CaptureFactory { get; set; } = null!;
    public static IFrameTimingSource FpsSource { get; set; } = null!;
    public static IFrameSource? CaptureSource { get; private set; }
    public static OverlayWindow? CurrentOverlay { get; set; }

    /// <summary>Fired after <see cref="PublishCaptureSource"/> wires up a new source.</summary>
    public static event Action<IFrameSource>? CaptureStarted;

    /// <summary>Every frame from the current <see cref="CaptureSource"/>. Tabs subscribe here.</summary>
    public static event Action<CapturedFrame>? FrameArrived;

    private static EventHandler<CapturedFrame>? _currentHandler;

    public static void PublishCaptureSource(IFrameSource source)
    {
        // Detach from prior source so old frames don't leak into new subscribers.
        if (CaptureSource is not null && _currentHandler is not null)
        {
            CaptureSource.FrameArrived -= _currentHandler;
        }

        CaptureSource = source;
        _currentHandler = (_, frame) => FrameArrived?.Invoke(frame);
        source.FrameArrived += _currentHandler;

        CaptureStarted?.Invoke(source);
    }
}
