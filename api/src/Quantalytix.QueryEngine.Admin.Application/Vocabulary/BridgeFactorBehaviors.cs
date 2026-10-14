namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>bridge_factor_behavior</c> vocabulary: how a bridge's factor affects a measure.</summary>
public static class BridgeFactorBehaviors
{
    /// <summary>The bridge factor does not modify the measure.</summary>
    public const string Ignore = "ignore";

    /// <summary>The signed factor multiplies the value contribution exactly once.</summary>
    public const string Value = "value";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "bridge_factor_behavior",
        Description = "How a bridge's factor affects a measure.",
        Values =
        [
            new() { Value = Ignore, Description = "The bridge factor does not modify the measure." },
            new() { Value = Value, Description = "The signed factor multiplies the value contribution exactly once." }
        ]
    };
}
