using System.Text;

namespace Game.Core.Diagnostics.Performance;

/// <summary>
/// Retains a bounded ring of finite, non-negative samples for explicit diagnostic capture.
/// Recording and capture reuse constructor-allocated arrays and do not perform file I/O.
/// </summary>
public sealed class LongSessionDistributionCollector
{
    private readonly double[] _samples;
    private readonly double[] _sortedSamples;
    private int _nextIndex;
    private int _count;
    private int _overBudgetSampleCount;
    private long _totalSampleCount;

    public LongSessionDistributionCollector(string label, int capacity, double budget)
    {
        LongSessionDistributionLabel.Validate(label);
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least one sample.");
        }

        if (!double.IsFinite(budget) || budget < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(budget), "Budget must be finite and non-negative.");
        }

        Label = label;
        Budget = budget;
        _samples = new double[capacity];
        _sortedSamples = new double[capacity];
    }

    public string Label { get; }

    public double Budget { get; }

    public int Capacity => _samples.Length;

    public int Count => _count;

    public long TotalSampleCount => _totalSampleCount;

    public void Add(double sample)
    {
        if (!double.IsFinite(sample) || sample < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sample), "Samples must be finite and non-negative.");
        }

        if (_count == _samples.Length && _samples[_nextIndex] > Budget)
        {
            _overBudgetSampleCount--;
        }

        _samples[_nextIndex] = sample;
        if (sample > Budget)
        {
            _overBudgetSampleCount++;
        }

        _nextIndex++;
        if (_nextIndex == _samples.Length)
        {
            _nextIndex = 0;
        }

        _count = Math.Min(_count + 1, _samples.Length);
        if (_totalSampleCount < long.MaxValue)
        {
            _totalSampleCount++;
        }
    }

    public LongSessionDistributionSnapshot Capture()
    {
        if (_count == 0)
        {
            return new LongSessionDistributionSnapshot(
                Label,
                Capacity,
                0,
                _totalSampleCount,
                Budget,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        CopyRetainedSamples();
        Array.Sort(_sortedSamples, 0, _count);

        var total = 0d;
        for (var index = 0; index < _count; index++)
        {
            total += _sortedSamples[index];
        }

        return new LongSessionDistributionSnapshot(
            Label,
            Capacity,
            _count,
            _totalSampleCount,
            Budget,
            total / _count,
            Percentile(0.50),
            Percentile(0.95),
            Percentile(0.99),
            _sortedSamples[_count - 1],
            _overBudgetSampleCount);
    }

    public void Clear()
    {
        Array.Clear(_samples);
        Array.Clear(_sortedSamples);
        _nextIndex = 0;
        _count = 0;
        _overBudgetSampleCount = 0;
        _totalSampleCount = 0;
    }

    private void CopyRetainedSamples()
    {
        var start = _count == _samples.Length ? _nextIndex : 0;
        for (var index = 0; index < _count; index++)
        {
            var sourceIndex = start + index;
            if (sourceIndex >= _samples.Length)
            {
                sourceIndex -= _samples.Length;
            }

            _sortedSamples[index] = _samples[sourceIndex];
        }
    }

    private double Percentile(double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * _count) - 1, 0, _count - 1);
        return _sortedSamples[index];
    }
}

public readonly record struct LongSessionDistributionSnapshot(
    string Label,
    int Capacity,
    int RetainedSampleCount,
    long TotalSampleCount,
    double Budget,
    double Average,
    double P50,
    double P95,
    double P99,
    double Maximum,
    int OverBudgetSampleCount)
{
    public double OverBudgetRatio =>
        RetainedSampleCount == 0 ? 0 : OverBudgetSampleCount / (double)RetainedSampleCount;
}

public static class LongSessionDistributionLabels
{
    public const string StreamingColdBidirectionalSettleMilliseconds =
        "streaming.camera-bidirectional.cold-settle-ms";

    public const string StreamingWarmBidirectionalSettleMilliseconds =
        "streaming.camera-bidirectional.warm-settle-ms";

    public const string SpawnFinalTreeTwoHundredMaintenanceMilliseconds =
        "spawn.final-tree-200.maintenance-ms";

    public const string AiFinalTreeTwoHundredUpdateMilliseconds =
        "simulation.final-tree-200.ai-update-ms";
}

public static class LongSessionDistributionLabel
{
    private const int MaximumLabelLength = 128;

    public static string Create(string domain, string scenario, string metric)
    {
        var canonicalDomain = CanonicalizeSegment(domain, nameof(domain));
        var canonicalScenario = CanonicalizeSegment(scenario, nameof(scenario));
        var canonicalMetric = CanonicalizeSegment(metric, nameof(metric));
        return string.Concat(canonicalDomain, ".", canonicalScenario, ".", canonicalMetric);
    }

    public static void Validate(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (label.Length > MaximumLabelLength)
        {
            throw new ArgumentException($"Distribution label must not exceed {MaximumLabelLength} characters.", nameof(label));
        }

        var hasSegmentValue = false;
        var previousWasSeparator = true;
        for (var index = 0; index < label.Length; index++)
        {
            var character = label[index];
            if (IsLowerAsciiLetter(character) || IsAsciiDigit(character))
            {
                hasSegmentValue = true;
                previousWasSeparator = false;
                continue;
            }

            if (character == '-')
            {
                if (previousWasSeparator)
                {
                    throw new ArgumentException("Distribution label separators must be surrounded by values.", nameof(label));
                }

                previousWasSeparator = true;
                continue;
            }

            if (character == '.')
            {
                if (!hasSegmentValue || previousWasSeparator)
                {
                    throw new ArgumentException("Distribution label segments must not be empty.", nameof(label));
                }

                hasSegmentValue = false;
                previousWasSeparator = true;
                continue;
            }

            throw new ArgumentException(
                "Distribution labels must use lowercase ASCII letters, digits, dots and hyphens.",
                nameof(label));
        }

        if (!hasSegmentValue || previousWasSeparator)
        {
            throw new ArgumentException("Distribution label must end with a value.", nameof(label));
        }
    }

    private static string CanonicalizeSegment(string segment, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segment, parameterName);
        var builder = new StringBuilder(segment.Length);
        var separatorPending = false;

        for (var index = 0; index < segment.Length; index++)
        {
            var character = segment[index];
            if (character is >= 'A' and <= 'Z')
            {
                AppendSeparatorIfNeeded(builder, ref separatorPending);
                builder.Append((char)(character + ('a' - 'A')));
            }
            else if (IsLowerAsciiLetter(character) || IsAsciiDigit(character))
            {
                AppendSeparatorIfNeeded(builder, ref separatorPending);
                builder.Append(character);
            }
            else if (character is '-' or '_' || char.IsWhiteSpace(character))
            {
                separatorPending = builder.Length > 0;
            }
            else
            {
                throw new ArgumentException(
                    "Distribution label segments must use ASCII letters, digits, whitespace, underscores or hyphens.",
                    parameterName);
            }
        }

        if (builder.Length == 0)
        {
            throw new ArgumentException("Distribution label segments must contain a letter or digit.", parameterName);
        }

        return builder.ToString();
    }

    private static void AppendSeparatorIfNeeded(StringBuilder builder, ref bool separatorPending)
    {
        if (separatorPending)
        {
            builder.Append('-');
            separatorPending = false;
        }
    }

    private static bool IsLowerAsciiLetter(char character)
    {
        return character is >= 'a' and <= 'z';
    }

    private static bool IsAsciiDigit(char character)
    {
        return character is >= '0' and <= '9';
    }
}
