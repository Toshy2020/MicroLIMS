namespace MicroLIMS.Application.Services.Spc;

// Statistical process control maths for one series of results, in date
// order: proposed alert/action limits, the moving-range chart, the eight
// Minitab/Nelson special-cause tests, process capability (Cp/Cpk/Pp/Ppk),
// a histogram and a box summary. Pure - no database, no clock - so every
// number the Trending charts draw is computed here, once, server-side.
public static class ControlChartCalculator
{
    // Bias-correction constants for a moving range of span w (index = w).
    private static readonly double[] D2 = { 0, 0, 1.128, 1.693, 2.059, 2.326, 2.534, 2.704, 2.847, 2.970, 3.078 };
    private static readonly double[] D3 = { 0, 0, 0, 0, 0, 0, 0, 0.076, 0.136, 0.184, 0.223 };
    private static readonly double[] D4 = { 0, 0, 3.267, 2.574, 2.282, 2.114, 2.004, 1.924, 1.864, 1.816, 1.777 };

    public static readonly IReadOnlyDictionary<int, string> RuleDescriptions = new Dictionary<int, string>
    {
        [1] = "1 point beyond the action limit",
        [2] = "9 points in a row on the same side of the mean",
        [3] = "6 points in a row, all increasing or all decreasing",
        [4] = "14 points in a row, alternating up and down",
        [5] = "2 out of 3 points more than 2σ from the mean (same side)",
        [6] = "4 out of 5 points more than 1σ from the mean (same side)",
        [7] = "15 points in a row within 1σ of the mean",
        [8] = "8 points in a row more than 1σ from the mean (either side)"
    };

    // values: the whole displayed series. inCalculation: which of them the
    // limits are estimated from (null = all). Rules are evaluated over the
    // whole series against the limits estimated from the calculation window.
    public static ControlChartComputation Compute(
        IReadOnlyList<double> values, ControlChartSettings settings, CapabilitySpec? spec = null, IReadOnlyList<bool>? inCalculation = null)
    {
        Validate(settings);
        var calc = inCalculation is null
            ? values.ToList()
            : values.Where((_, i) => inCalculation[i]).ToList();
        var n = calc.Count;
        var span = settings.MovingRangeLength;
        var movingRanges = MovingRanges(values, span);

        if (n == 0)
        {
            return new ControlChartComputation(
                0, null, null, null, null, null, null, null, null, null, null, settings.MinimumPoints > 0,
                new ProposedLimits(null, null, null, null), movingRanges, values.Select(_ => Array.Empty<int>()).ToList(),
                new CapabilityIndices(spec?.Lsl, spec?.Usl, null, null, null, null, null, null, null, null),
                new List<HistogramBin>(), null);
        }

        var mean = calc.Average();
        var sd = n > 1 ? Math.Sqrt(calc.Sum(v => (v - mean) * (v - mean)) / (n - 1)) : 0;

        var calcRanges = MovingRanges(calc, span).Where(r => r.HasValue).Select(r => r!.Value).ToList();
        double? mrBar = calcRanges.Count > 0 ? calcRanges.Average() : null;
        double? sigmaWithin = mrBar.HasValue ? mrBar.Value / D2[span] : null;
        var sigmaUsed = settings.Method == LimitMethod.StandardDeviation ? sd : sigmaWithin;

        var sorted = calc.OrderBy(v => v).ToList();
        var limits = ProposeLimits(settings, mean, sigmaUsed, sorted);
        // Counts and other non-negative results can't fall below zero, so
        // neither can their lower limits.
        if (values.All(v => v >= 0))
            limits = limits with { AlertLower = Floor0(limits.AlertLower), ActionLower = Floor0(limits.ActionLower) };

        // Rules 2-8 need a spread: the chosen σ, or the moving-range σ for
        // the percentile method (which has no σ of its own).
        var ruleSigma = settings.Method == LimitMethod.Percentile ? sigmaWithin : sigmaUsed;
        var violations = EvaluateRules(values, mean, ruleSigma, limits.ActionUpper, limits.ActionLower, settings.EnabledRules);

        return new ControlChartComputation(
            Count: n,
            Mean: mean,
            Median: Percentile(sorted, 50),
            Min: sorted[0],
            Max: sorted[^1],
            StandardDeviation: sd,
            SigmaWithin: sigmaWithin,
            SigmaUsed: sigmaUsed,
            MovingRangeMean: mrBar,
            MovingRangeUcl: mrBar * D4[span],
            MovingRangeLcl: mrBar * D3[span],
            BelowMinimumPoints: n < settings.MinimumPoints,
            Limits: limits,
            MovingRanges: movingRanges,
            RuleViolations: violations,
            // Indices describe the in-control process; the observed share
            // outside counts every charted result, excluded OOS ones included.
            Capability: Capability(calc, mean, sd, sigmaWithin, spec, values),
            Histogram: Histogram(sorted, mean, sd),
            Box: Box(sorted));
    }

    public static void Validate(ControlChartSettings s)
    {
        if (s.MovingRangeLength is < 2 or > 10)
            throw new InvalidOperationException("Moving range length must be between 2 and 10.");
        if (s.Method == LimitMethod.Percentile)
        {
            if (s.AlertPercentile is < 0 or > 100 || s.ActionPercentile is < 0 or > 100)
                throw new InvalidOperationException("Percentiles must be between 0 and 100.");
            if (s.AlertPercentile > s.ActionPercentile)
                throw new InvalidOperationException("The alert percentile must not be above the action percentile.");
        }
        else
        {
            if (s.AlertK <= 0 || s.ActionK <= 0)
                throw new InvalidOperationException("The alert and action multipliers (k) must be greater than zero.");
            if (s.AlertK > s.ActionK)
                throw new InvalidOperationException("The alert multiplier (k) must not be above the action multiplier.");
        }
        if (s.MinimumPoints < 0)
            throw new InvalidOperationException("Minimum data points cannot be negative.");
        if (s.EnabledRules.Any(r => r is < 1 or > 8))
            throw new InvalidOperationException("Control chart rules are numbered 1 to 8.");
    }

    // Span-w moving range: max - min of each run of w consecutive values,
    // aligned to the last value of the run (the first w-1 have none).
    public static List<double?> MovingRanges(IReadOnlyList<double> values, int span)
    {
        var result = new List<double?>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            if (i < span - 1) { result.Add(null); continue; }
            double max = double.MinValue, min = double.MaxValue;
            for (var j = i - span + 1; j <= i; j++)
            {
                if (values[j] > max) max = values[j];
                if (values[j] < min) min = values[j];
            }
            result.Add(max - min);
        }
        return result;
    }

    // PERCENTILE.INC: linear interpolation between closest ranks.
    public static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) throw new InvalidOperationException("No values.");
        var rank = Math.Clamp(p, 0, 100) / 100 * (sorted.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
    }

    public static double? Round(double? value, LimitRounding rounding, bool lower = false)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return null;
        var v = value.Value;
        return rounding switch
        {
            // Small tolerance so 5.0000000001 from floating-point noise stays 5.
            LimitRounding.UpToWholeNumber => lower ? Math.Floor(v + 1e-9) : Math.Ceiling(v - 1e-9),
            LimitRounding.OneDecimal => Math.Round(v, 1, MidpointRounding.AwayFromZero),
            LimitRounding.TwoDecimals => Math.Round(v, 2, MidpointRounding.AwayFromZero),
            _ => Math.Round(v, 4, MidpointRounding.AwayFromZero)
        };
    }

    private static double? Floor0(double? v) => v is null ? null : Math.Max(0, v.Value);

    private static ProposedLimits ProposeLimits(ControlChartSettings s, double mean, double? sigma, IReadOnlyList<double> sorted)
    {
        if (s.Method == LimitMethod.Percentile)
        {
            return new ProposedLimits(
                Round(Percentile(sorted, s.AlertPercentile), s.Rounding),
                Round(Percentile(sorted, s.ActionPercentile), s.Rounding),
                null, null);
        }
        if (sigma is null) return new ProposedLimits(null, null, null, null);
        return new ProposedLimits(
            Round(mean + s.AlertK * sigma, s.Rounding),
            Round(mean + s.ActionK * sigma, s.Rounding),
            Round(mean - s.AlertK * sigma, s.Rounding, lower: true),
            Round(mean - s.ActionK * sigma, s.Rounding, lower: true));
    }

    // The eight Minitab special-cause tests. Each flagged point lists the
    // numbers of the tests it fails; a point is flagged at the END of the
    // run that completes the pattern.
    public static List<int[]> EvaluateRules(
        IReadOnlyList<double> v, double mean, double? sigma, double? actionUpper, double? actionLower, IReadOnlyCollection<int> rules)
    {
        var flags = v.Select(_ => new SortedSet<int>()).ToList();
        var hasSigma = sigma is > 0;
        double Z(int i) => hasSigma ? (v[i] - mean) / sigma!.Value : 0;

        bool Run(int end, int length, Func<int, bool> predicate)
        {
            if (end < length - 1) return false;
            for (var j = end - length + 1; j <= end; j++)
                if (!predicate(j)) return false;
            return true;
        }

        int CountIn(int end, int length, Func<int, bool> predicate)
        {
            var c = 0;
            for (var j = Math.Max(0, end - length + 1); j <= end; j++)
                if (predicate(j)) c++;
            return c;
        }

        for (var k = 0; k < v.Count; k++)
        {
            if (rules.Contains(1) && ((actionUpper.HasValue && v[k] > actionUpper) || (actionLower.HasValue && v[k] < actionLower)))
                flags[k].Add(1);
            if (rules.Contains(2) && (Run(k, 9, j => v[j] > mean) || Run(k, 9, j => v[j] < mean)))
                flags[k].Add(2);
            // 6 points = 5 consecutive steps in the same direction.
            if (rules.Contains(3) && k >= 5 && (Run(k, 5, j => v[j] > v[j - 1]) || Run(k, 5, j => v[j] < v[j - 1])))
                flags[k].Add(3);
            // 14 points = 13 steps, each reversing the one before (12 reversals).
            if (rules.Contains(4) && k >= 13 && Run(k, 12, j => (v[j] - v[j - 1]) * (v[j - 1] - v[j - 2]) < 0))
                flags[k].Add(4);

            if (!hasSigma) continue;
            if (rules.Contains(5) && k >= 2 &&
                ((Z(k) > 2 && CountIn(k, 3, j => Z(j) > 2) >= 2) || (Z(k) < -2 && CountIn(k, 3, j => Z(j) < -2) >= 2)))
                flags[k].Add(5);
            if (rules.Contains(6) && k >= 4 &&
                ((Z(k) > 1 && CountIn(k, 5, j => Z(j) > 1) >= 4) || (Z(k) < -1 && CountIn(k, 5, j => Z(j) < -1) >= 4)))
                flags[k].Add(6);
            if (rules.Contains(7) && Run(k, 15, j => Math.Abs(Z(j)) < 1))
                flags[k].Add(7);
            if (rules.Contains(8) && Run(k, 8, j => Math.Abs(Z(j)) > 1))
                flags[k].Add(8);
        }
        return flags.Select(f => f.ToArray()).ToList();
    }

    // Cp/Cpk use the within (moving range) σ, Pp/Ppk the overall standard
    // deviation. With only an upper limit - the usual case for microbial
    // counts - Cp/Pp are undefined and Cpk = Cpu.
    public static CapabilityIndices Capability(IReadOnlyList<double> values, double mean, double overallSd, double? sigmaWithin, CapabilitySpec? spec,
        IReadOnlyList<double>? observed = null)
    {
        observed ??= values;
        var lsl = spec?.Lsl;
        var usl = spec?.Usl;
        if (lsl is null && usl is null || values.Count == 0)
            return new CapabilityIndices(lsl, usl, null, null, null, null, null, null, null, null);

        static double? Ratio(double? numerator, double? sigma, double factor) =>
            numerator.HasValue && sigma is > 0 ? numerator.Value / (factor * sigma.Value) : null;

        var cp = lsl.HasValue && usl.HasValue ? Ratio(usl - lsl, sigmaWithin, 6) : null;
        var cpu = Ratio(usl - mean, sigmaWithin, 3);
        var cpl = Ratio(mean - lsl, sigmaWithin, 3);
        var pp = lsl.HasValue && usl.HasValue ? Ratio(usl - lsl, overallSd, 6) : null;
        var ppu = Ratio(usl - mean, overallSd, 3);
        var ppl = Ratio(mean - lsl, overallSd, 3);

        var outside = observed.Count(x => (usl.HasValue && x > usl) || (lsl.HasValue && x < lsl));
        double? expected = null;
        if (overallSd > 0)
        {
            var above = usl.HasValue ? 1 - NormalCdf((usl.Value - mean) / overallSd) : 0;
            var below = lsl.HasValue ? NormalCdf((lsl.Value - mean) / overallSd) : 0;
            expected = (above + below) * 100;
        }

        return new CapabilityIndices(
            lsl, usl,
            Cp: cp, Cpk: MinOf(cpu, cpl), Cpu: cpu, Cpl: cpl,
            Pp: pp, Ppk: MinOf(ppu, ppl),
            ObservedPercentOutside: 100.0 * outside / observed.Count,
            ExpectedPercentOutside: expected);
    }

    // sqrt(n) bins (5 to 20); integer data - plate counts - get whole-number
    // bin widths so a bar never straddles two counts. ExpectedCount is the
    // normal curve with the same mean and SD, for the capability histogram.
    public static List<HistogramBin> Histogram(IReadOnlyList<double> sorted, double mean, double sd)
    {
        if (sorted.Count == 0) return new List<HistogramBin>();
        var min = sorted[0];
        var max = sorted[^1];
        var bins = Math.Clamp((int)Math.Ceiling(Math.Sqrt(sorted.Count)), 5, 20);
        var integers = sorted.All(x => Math.Abs(x - Math.Round(x)) < 1e-9);

        double start, width;
        if (integers)
        {
            width = Math.Max(1, Math.Ceiling((max - min + 1) / bins));
            start = min - 0.5;
            bins = (int)Math.Ceiling((max - min + 1) / width);
        }
        else if (max - min < 1e-12)
        {
            width = 1;
            start = min - 0.5;
            bins = 1;
        }
        else
        {
            width = (max - min) / bins;
            start = min;
        }

        var result = new List<HistogramBin>(bins);
        for (var b = 0; b < bins; b++)
        {
            var from = start + b * width;
            var to = from + width;
            var last = b == bins - 1;
            var count = sorted.Count(x => x >= from && (last ? x <= to : x < to));
            double? expected = sd > 0
                ? sorted.Count * (NormalCdf((to - mean) / sd) - NormalCdf((from - mean) / sd))
                : null;
            result.Add(new HistogramBin(from, to, count, expected));
        }
        return result;
    }

    // Tukey box: whiskers reach the furthest points within 1.5 × IQR.
    public static BoxSummary? Box(IReadOnlyList<double> sorted)
    {
        if (sorted.Count == 0) return null;
        var q1 = Percentile(sorted, 25);
        var q3 = Percentile(sorted, 75);
        var iqr = q3 - q1;
        var lowFence = q1 - 1.5 * iqr;
        var highFence = q3 + 1.5 * iqr;
        var inside = sorted.Where(x => x >= lowFence && x <= highFence).ToList();
        return new BoxSummary(
            sorted[0], q1, Percentile(sorted, 50), q3, sorted[^1],
            inside.Count > 0 ? inside[0] : sorted[0],
            inside.Count > 0 ? inside[^1] : sorted[^1],
            sorted.Where(x => x < lowFence || x > highFence).ToList());
    }

    private static double? MinOf(double? a, double? b) =>
        a.HasValue && b.HasValue ? Math.Min(a.Value, b.Value) : a ?? b;

    // Standard normal CDF via the Abramowitz & Stegun 7.1.26 erf
    // approximation (absolute error < 1.5e-7).
    public static double NormalCdf(double z)
    {
        var x = Math.Abs(z) / Math.Sqrt(2);
        var t = 1 / (1 + 0.3275911 * x);
        var erf = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return z >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
    }
}
