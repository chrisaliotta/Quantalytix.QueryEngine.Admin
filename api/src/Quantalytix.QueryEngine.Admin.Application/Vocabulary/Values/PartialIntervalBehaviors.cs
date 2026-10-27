namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>partial_interval_behavior</c> vocabulary: how an interval only partly inside the requested range is weighted.</summary>
public static class PartialIntervalBehaviors
{
    /// <summary>Never estimate; weigh each interval by its whole duration in the period it starts in (the default).</summary>
    public const string ExactOnly = "exact_only";

    /// <summary>Permit an approved uniform-accrual estimate; result metadata identifies the estimate.</summary>
    public const string UniformEstimate = "uniform_estimate";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "partial_interval_behavior",
        Key = "partialIntervalBehavior",
        Description = "How an interval only partly inside the requested range is weighted.",
        Values =
        [
            new() { Value = ExactOnly, Label = "Exact only", Description = "Never estimate; weigh each interval by its whole duration in the period it starts in (the default)." },
            new() { Value = UniformEstimate, Label = "Uniform estimate", Description = "Permit an approved uniform-accrual estimate; result metadata identifies the estimate." }
        ]
    };
}
