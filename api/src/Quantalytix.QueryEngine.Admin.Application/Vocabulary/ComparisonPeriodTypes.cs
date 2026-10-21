namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>comparison_period_type</c> vocabulary, the <c>type</c> of a report comparison's <c>period</c>: how each run period maps
/// to the one period the comparison reads.
/// </summary>
public static class ComparisonPeriodTypes
{
    /// <summary>The same dates moved back by a <c>length</c> of a <c>unit</c>, such as year over year.</summary>
    public const string Shift = "shift";

    /// <summary>A whole period of a <c>periodicity</c>, an <c>offset</c> back from the one that contains the run period.</summary>
    public const string Period = "period";

    /// <summary>The last day of such a period, read at day periodicity.</summary>
    public const string PeriodEnd = "periodEnd";

    /// <summary>One fixed baseline from <c>start</c> to <c>endExclusive</c>.</summary>
    public const string Range = "range";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "comparison_period_type",
        Description = "How a report comparison maps each run period to the period it reads.",
        Values =
        [
            new() { Value = Shift, Description = "The same dates moved back by a length of a unit, such as year over year." },
            new() { Value = Period, Description = "A whole period of a periodicity, an offset back from the one that contains the run period." },
            new() { Value = PeriodEnd, Description = "The last day of such a period, read at day periodicity." },
            new() { Value = Range, Description = "One fixed baseline from start to endExclusive." }
        ]
    };
}
