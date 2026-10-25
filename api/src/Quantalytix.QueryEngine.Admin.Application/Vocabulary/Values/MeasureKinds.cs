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

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "measure_kind",
        Description = "How a measure computes its value.",
        Values =
        [
            new() { Value = Aggregate, Description = "Ordinary aggregate over eligible source rows or selected observations." },
            new() { Value = WeightedSum, Description = "SUM(value * weight) after temporal and bridge policies." },
            new() { Value = WeightedAverage, Description = "SUM(value * weight) / SUM(weight) over the same eligible population." },
            new() { Value = PeriodAverage, Description = "Aggregate within each native interval, then duration-weight the interval series." },
            new() { Value = Formula, Description = "Validated expression over other measures of the same dataset version." },
            new() { Value = Specialized, Description = "Allowlisted domain implementation with versioned, validated configuration." }
        ]
    };
}
