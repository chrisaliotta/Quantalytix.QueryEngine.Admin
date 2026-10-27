namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>total_behavior</c> vocabulary: how totals of a measure are formed.</summary>
public static class TotalBehaviors
{
    /// <summary>Recompute the measure from mergeable state at the total grain; required for ratios and nonlinear measures.</summary>
    public const string Recompute = "recompute";

    /// <summary>Sum already-resolved child values; only valid for additive measures.</summary>
    public const string SumChildren = "sum_children";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "total_behavior",
        Key = "totalBehavior",
        Description = "How totals of a measure are formed.",
        Values =
        [
            new() { Value = Recompute, Label = "Recompute", Description = "Recompute the measure from mergeable state at the total grain; required for ratios and nonlinear measures." },
            new() { Value = SumChildren, Label = "Sum children", Description = "Sum already-resolved child values; only valid for additive measures." }
        ]
    };
}
