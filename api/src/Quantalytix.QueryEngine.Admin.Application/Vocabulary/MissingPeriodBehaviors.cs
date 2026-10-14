namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>missing_period_behavior</c> vocabulary: what a measure outputs for a period without observations.</summary>
public static class MissingPeriodBehaviors
{
    /// <summary>Output null for the affected period (the default).</summary>
    public const string Null = "null";

    /// <summary>Treat the missing period as zero, only where business semantics justify it.</summary>
    public const string Zero = "zero";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "missing_period_behavior",
        Description = "What a measure outputs for a period without observations.",
        Values =
        [
            new() { Value = Null, Description = "Output null for the affected period (the default)." },
            new() { Value = Zero, Description = "Treat the missing period as zero, only where business semantics justify it." }
        ]
    };
}
