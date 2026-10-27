namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>variable_missing_behavior</c> vocabulary: the value a variable takes where its source has no data.</summary>
public static class VariableMissingBehaviors
{
    /// <summary>The value is null, and so is every formula that reads it.</summary>
    public const string Null = "null";

    /// <summary>The value is zero.</summary>
    public const string Zero = "zero";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "variable_missing_behavior",
        Key = "variableMissingBehavior",
        Description = "The value a variable takes where its source has no data.",
        Values =
        [
            new() { Value = Null, Label = "Null", Description = "The value is null, and so is every formula that reads it." },
            new() { Value = Zero, Label = "Zero", Description = "The value is zero." }
        ]
    };
}
