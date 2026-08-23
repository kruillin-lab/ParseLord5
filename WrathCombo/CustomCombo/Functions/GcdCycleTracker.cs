namespace WrathCombo.CustomComboNS.Functions;

internal sealed class GcdCycleTracker(float activeThreshold = 0.1f)
{
    private bool _rolling;

    public bool TryUpdate(float remainingGcd, out bool rolling)
    {
        rolling = remainingGcd >= activeThreshold;
        if (rolling == _rolling)
            return false;

        _rolling = rolling;
        return true;
    }
}
