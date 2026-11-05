namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>measure_kind</c> vocabulary: how a measure computes its value.</summary>
public static class MeasureKinds
{
    /// <summary>Ordinary aggregate over eligible source rows or selected observations.</summary>
    public const string Aggregate = "aggregate";

    /// <summary>SUM(value * weight) after temporal and bridge policies.</summary>
    public const string WeightedSum = "weighted_sum";

    /// <summary>SUM(value * weight) / SUM(weight) over the same eligible population.</summary>
    public const string WeightedAverage = "weighted_average";

    /// <summary>Aggregate within each native interval, then duration-weight the interval series.</summary>
    public const string PeriodAverage = "period_average";

    /// <summary>Validated expression over other measures of the same dataset version.</summary>
    public const string Formula = "formula";

    /// <summary>Allowlisted domain implementation with versioned, validated configuration.</summary>
    public const string Specialized = "specialized";

    /// <summary>Per-step rate over other measures, linked geometrically across the steps of each period.</summary>
    public const string Compound = "compound";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "measure_kind",
        Key = "measureKind",
        Description = "How a measure computes its value.",
        Values =
        [
            new() { Value = Aggregate, Label = "Aggregate", Description = "Ordinary aggregate over eligible source rows or selected observations." },
            new() { Value = WeightedSum, Label = "Weighted sum", Description = "SUM(value * weight) after temporal and bridge policies." },
            new() { Value = WeightedAverage, Label = "Weighted average", Description = "SUM(value * weight) / SUM(weight) over the same eligible population." },
            new() { Value = PeriodAverage, Label = "Period average", Description = "Aggregate within each native interval, then duration-weight the interval series." },
            new() { Value = Formula, Label = "Formula", Description = "Validated expression over other measures of the same dataset version." },
            new() { Value = Specialized, Label = "Specialized", Description = "Allowlisted domain implementation with versioned, validated configuration." },
            new() { Value = Compound, Label = "Compound", Description = "Per-step rate over other measures, linked geometrically across the steps of each period." }
        ]
    };

    /// <summary>Whether a kind's expression composes other measures (<c>[m.key]</c>): formula and compound measures.</summary>
    /// <param name="measureKind">A <c>measure_kind</c> value.</param>
    /// <returns>True for <see cref="Formula"/> and <see cref="Compound"/>.</returns>
    public static bool ComposesMeasures(string measureKind)
    {
        return measureKind is Formula or Compound;
    }
}
