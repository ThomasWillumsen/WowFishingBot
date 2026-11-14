namespace DeskHelper.Infrastructure;

/// <summary>
/// Flags values that jump well above a slowly adapting baseline, so a loud-but-steady environment
/// doesn't trigger it the way a fixed threshold would.
/// </summary>
internal sealed class SpikeDetector(double minimum, double factor, int warmupSamples = 20, double adaptation = 0.01)
{
    private double _baseline;
    private int _samples;

    public double Baseline => _baseline;

    /// <summary>Feeds one measurement and returns true if it is a spike: at least <c>minimum</c> and <c>factor</c> times the baseline.</summary>
    public bool Update(double value)
    {
        _samples++;

        if (_samples <= warmupSamples)
        {
            // running mean while the baseline is still being learned
            _baseline += (value - _baseline) / _samples;
            return false;
        }

        var threshold = Math.Max(minimum, _baseline * factor);

        // spikes only pull the baseline up to the threshold, so a lasting change in the environment is absorbed gradually
        _baseline += adaptation * (Math.Min(value, threshold) - _baseline);

        return value >= threshold;
    }
}
