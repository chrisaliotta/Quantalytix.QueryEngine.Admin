namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>component_role</c> vocabulary: the role a source field plays in a measure.</summary>
public static class ComponentRoles
{
    /// <summary>Primary numeric value of most aggregates, weighted measures and period averages.</summary>
    public const string Value = "value";

    /// <summary>Statistical weight for weighted sums and weighted averages.</summary>
    public const string Weight = "weight";

    /// <summary>Optional verification field for a source-supplied period duration; never summed blindly.</summary>
    public const string PeriodDays = "period_days";

    /// <summary>Optional precomputed mergeable numerator state for specialized plans.</summary>
    public const string Numerator = "numerator";

    /// <summary>Optional precomputed mergeable denominator state for specialized plans.</summary>
    public const string Denominator = "denominator";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "component_role",
        Key = "componentRole",
        Description = "The role a source field plays in a measure.",
        Values =
        [
            new() { Value = Value, Label = "Value", Description = "Primary numeric value of most aggregates, weighted measures and period averages." },
            new() { Value = Weight, Label = "Weight", Description = "Statistical weight for weighted sums and weighted averages." },
            new() { Value = PeriodDays, Label = "Period days", Description = "Optional verification field for a source-supplied period duration; never summed blindly." },
            new() { Value = Numerator, Label = "Numerator", Description = "Optional precomputed mergeable numerator state for specialized plans." },
            new() { Value = Denominator, Label = "Denominator", Description = "Optional precomputed mergeable denominator state for specialized plans." }
        ]
    };
}
