namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>cumulative_origin</c> vocabulary: where cumulative calculations over a time binding begin.</summary>
public static class CumulativeOrigins
{
    /// <summary>Cumulative calculations begin at the request's output range start.</summary>
    public const string OutputRangeStart = "output_range_start";

    /// <summary>Cumulative calculations begin at the binding's cumulative fixed start.</summary>
    public const string FixedStart = "fixed_start";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "cumulative_origin",
        Description = "Where cumulative calculations over a time binding begin.",
        Values =
        [
            new() { Value = OutputRangeStart, Description = "Cumulative calculations begin at the request's output range start." },
            new() { Value = FixedStart, Description = "Cumulative calculations begin at the binding's cumulative fixed start." }
        ]
    };
}
