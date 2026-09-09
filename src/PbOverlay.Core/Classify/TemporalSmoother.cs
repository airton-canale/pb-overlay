namespace PbOverlay.Core.Classify;

public sealed class TemporalSmoother
{
    private readonly int _slotCount;
    private readonly int _agreementFrames;
    private readonly SlotState[] _stable;
    private readonly SlotState[] _candidate;
    private readonly int[] _streak;

    public TemporalSmoother(int slotCount, int agreementFrames = 3)
    {
        if (slotCount < 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
        if (agreementFrames < 1) throw new ArgumentOutOfRangeException(nameof(agreementFrames));
        _slotCount = slotCount;
        _agreementFrames = agreementFrames;
        _stable = new SlotState[slotCount];
        _candidate = new SlotState[slotCount];
        _streak = new int[slotCount];
    }

    public IReadOnlyList<SlotState> Update(IReadOnlyList<SlotState> observed)
    {
        if (observed.Count != _slotCount)
            throw new ArgumentException($"Expected {_slotCount} observations, got {observed.Count}.", nameof(observed));

        for (var i = 0; i < _slotCount; i++)
        {
            var incoming = observed[i];
            if (incoming == _stable[i])
            {
                _candidate[i] = _stable[i];
                _streak[i] = 0;
            }
            else if (incoming == _candidate[i])
            {
                _streak[i]++;
                if (_streak[i] >= _agreementFrames)
                {
                    _stable[i] = _candidate[i];
                    _streak[i] = 0;
                }
            }
            else
            {
                _candidate[i] = incoming;
                _streak[i] = 1;
            }
        }

        return (SlotState[])_stable.Clone();
    }
}
