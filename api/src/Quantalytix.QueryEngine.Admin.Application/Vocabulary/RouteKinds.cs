namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>route_kind</c> vocabulary: how a calculation dimension route reads its field.</summary>
public static class RouteKinds
{
    /// <summary>The field's binding resolves canonical members.</summary>
    public const string Semantic = "semantic";

    /// <summary>The field's raw values.</summary>
    public const string Field = "field";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "route_kind",
        Description = "How a calculation dimension route reads its field.",
        Values =
        [
            new() { Value = Semantic, Description = "The field's binding resolves canonical members." },
            new() { Value = Field, Description = "The field's raw values." }
        ]
    };
}
