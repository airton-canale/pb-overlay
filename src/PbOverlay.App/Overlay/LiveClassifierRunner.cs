using OpenCvSharp;
using PbOverlay.Core.Capture;
using PbOverlay.Core.Classify;
using PbOverlay.Core.Roi;
using Serilog;

namespace PbOverlay.App.Overlay;

/// <summary>
/// Always-on classifier: subscribes to <see cref="AppServices.FrameArrived"/>,
/// runs the pipeline with current config, and pushes the alive-counts to the
/// visible overlay. Runs regardless of which tab is open.
/// </summary>
internal sealed class LiveClassifierRunner
{
    private SaturationStateClassifier? _classifier;
    private ClassifierPipeline? _pipeline;
    private int _slotCount = -1;
    private double _lastSat = double.NaN;
    private double _lastVar = double.NaN;
    private int _lastFrames = -1;
    private int _busy;

    public LiveClassifierRunner()
    {
        AppServices.FrameArrived += OnFrame;
    }

    private void OnFrame(CapturedFrame frame)
    {
        if (System.Threading.Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var profile = SelectedProfile();
            if (profile is null || !profile.IsCalibrated) return;

            EnsurePipeline(profile.SlotsPerTeam);
            var (aRects, bRects) = RoiMapper.SliceProfile(profile, frame.Width, frame.Height);

            // Clamp to frame bounds; OpenCV's Mat(Mat, Rect) throws (native AV
            // risk) on out-of-range rects, which happens when calibrated ROIs
            // don't match the current game frame size.
            var frameRect = new Rect(0, 0, frame.Width, frame.Height);
            aRects = ClampAll(aRects, frameRect);
            bRects = ClampAll(bRects, frameRect);

            var (_, _, aAlive, bAlive) = _pipeline!.Process(frame.Bgra, aRects, bRects);
            AppServices.CurrentOverlay?.SetCount(aAlive, bAlive);
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "LiveClassifierRunner frame failed");
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _busy, 0);
        }
    }

    private void EnsurePipeline(int slotsPerTeam)
    {
        var c = AppServices.Config.Classifier;
        if (_pipeline is not null
            && _slotCount == slotsPerTeam
            && _lastSat == c.AliveDeadSaturationThreshold
            && _lastVar == c.EmptyVarianceThreshold
            && _lastFrames == c.TemporalSmoothingFrames)
        {
            return;
        }

        _classifier = new SaturationStateClassifier(
            new LowVarianceEmptyDetector(c.EmptyVarianceThreshold),
            c.AliveDeadSaturationThreshold);
        int frames = Math.Max(1, c.TemporalSmoothingFrames);
        _pipeline = new ClassifierPipeline(
            _classifier,
            new TemporalSmoother(slotsPerTeam, frames),
            new TemporalSmoother(slotsPerTeam, frames));
        _slotCount = slotsPerTeam;
        _lastSat = c.AliveDeadSaturationThreshold;
        _lastVar = c.EmptyVarianceThreshold;
        _lastFrames = c.TemporalSmoothingFrames;
    }

    private static IReadOnlyList<Rect> ClampAll(IReadOnlyList<Rect> rects, Rect bounds)
    {
        var result = new Rect[rects.Count];
        for (int i = 0; i < rects.Count; i++) result[i] = rects[i].Intersect(bounds);
        return result;
    }

    private static RoiProfile? SelectedProfile()
    {
        var profiles = AppServices.Config.RoiProfiles;
        return profiles.Count > 0 ? profiles[0] : null;
    }
}
