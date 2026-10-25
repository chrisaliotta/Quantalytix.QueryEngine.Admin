namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>bridge_allocation_behavior</c> vocabulary: how a bridge's allocation weight affects a measure.</summary>
public static class BridgeAllocationBehaviors
{
    /// <summary>Apply allocation exactly once, to the contribution or the effective statistical weight.</summary>
    public const string Apply = "apply";

    /// <summary>Ignore allocation; only valid for an explicitly non-additive implementation.</summary>
    public const string Ignore = "ignore";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "bridge_allocation_behavior",
        Description = "How a bridge's allocation weight affects a measure.",
        Values =
        [
            new() { Value = Apply, Description = "Apply allocation exactly once, to the contribution or the effective statistical weight." },
            new() { Value = Ignore, Description = "Ignore allocation; only valid for an explicitly non-additive implementation." }
        ]
    };
}
