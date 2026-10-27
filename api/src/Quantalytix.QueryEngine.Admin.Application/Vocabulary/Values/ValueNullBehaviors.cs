namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>value_null_behavior</c> vocabulary: how a measure treats a null value.</summary>
public static class ValueNullBehaviors
{
    /// <summary>Remove the row or pair from the calculation, from numerator and denominator together (the default).</summary>
    public const string Exclude = "exclude";

    /// <summary>Treat a null value as zero while keeping the other eligibility rules.</summary>
    public const string Zero = "zero";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "value_null_behavior",
        Key = "valueNullBehavior",
        Description = "How a measure treats a null value.",
        Values =
        [
            new() { Value = Exclude, Label = "Exclude", Description = "Remove the row or pair from the calculation, from numerator and denominator together (the default)." },
            new() { Value = Zero, Label = "Zero", Description = "Treat a null value as zero while keeping the other eligibility rules." }
        ]
    };
}
