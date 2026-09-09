using OpenCvSharp;

namespace PbOverlay.Core.Classify;

public sealed class ClassifierPipeline
{
    private readonly IStateClassifierBatch _classifier;
    private readonly TemporalSmoother _teamASmoother;
    private readonly TemporalSmoother _teamBSmoother;

    public ClassifierPipeline(
        IStateClassifierBatch classifier,
        TemporalSmoother teamASmoother,
        TemporalSmoother teamBSmoother)
    {
        _classifier = classifier;
        _teamASmoother = teamASmoother;
        _teamBSmoother = teamBSmoother;
    }

    public IStateClassifierBatch Classifier => _classifier;

    public (IReadOnlyList<SlotState> teamA, IReadOnlyList<SlotState> teamB, int aliveA, int aliveB) Process(
        Mat frameBgra,
        IReadOnlyList<Rect> teamASlots,
        IReadOnlyList<Rect> teamBSlots)
    {
        var rawA = _classifier.ClassifyAll(frameBgra, teamASlots);
        var smoothedA = _teamASmoother.Update(rawA);

        var rawB = _classifier.ClassifyAll(frameBgra, teamBSlots);
        var smoothedB = _teamBSmoother.Update(rawB);

        return (smoothedA, smoothedB, CountAlive(smoothedA), CountAlive(smoothedB));
    }

    private static int CountAlive(IReadOnlyList<SlotState> states)
    {
        var n = 0;
        for (var i = 0; i < states.Count; i++) if (states[i] == SlotState.Alive) n++;
        return n;
    }
}
