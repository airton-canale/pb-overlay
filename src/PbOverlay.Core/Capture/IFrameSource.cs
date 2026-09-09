namespace PbOverlay.Core.Capture;

public interface IFrameSource : IAsyncDisposable
{
    /// <summary>Fired on the producer thread when a frame is ready.</summary>
    event EventHandler<CapturedFrame>? FrameArrived;

    /// <summary>Fired when the underlying capture target goes away
    /// (window closed, monitor unplugged, adapter reset). The source stops
    /// producing frames until <see cref="StartAsync"/> is called again.</summary>
    event EventHandler? TargetLost;

    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
}
