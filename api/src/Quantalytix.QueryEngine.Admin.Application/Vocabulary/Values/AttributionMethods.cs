namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>attribution_method</c> vocabulary: how a model splits a metric's change between two values across its drivers.</summary>
public static class AttributionMethods
{
    /// <summary>Each driver's effect averaged over every order of switching the drivers (the default).</summary>
    public const string Shapley = "shapley";

    /// <summary>The drivers switched one at a time, in their sort order.</summary>
    public const string Sequential = "sequential";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "attribution_method",
        Description = "How a metric's change between two values splits into the effects of its drivers.",
        Values =
        [
            new() { Value = Shapley, Description = "Each driver's effect averaged over every order of switching the drivers (the default)." },
            new() { Value = Sequential, Description = "The drivers switched one at a time, in their sort order." }
        ]
    };
}
