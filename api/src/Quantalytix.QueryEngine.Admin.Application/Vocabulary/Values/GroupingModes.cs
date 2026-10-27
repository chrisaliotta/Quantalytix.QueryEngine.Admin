namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>grouping_mode</c> vocabulary: what a binding groups resolved members by.</summary>
public static class GroupingModes
{
    /// <summary>Group by the resolved member itself.</summary>
    public const string Member = "member";

    /// <summary>Resolve each member to its unique ancestor at the grouping level.</summary>
    public const string Level = "level";

    /// <summary>Group an intended subtree at a fixed ancestor member.</summary>
    public const string Ancestor = "ancestor";

    /// <summary>Resolve to an ancestor at an explicit closure depth.</summary>
    public const string Depth = "depth";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "grouping_mode",
        Key = "groupingMode",
        Description = "What a binding groups resolved members by.",
        Values =
        [
            new() { Value = Member, Label = "Member", Description = "Group by the resolved member itself." },
            new() { Value = Level, Label = "Level", Description = "Resolve each member to its unique ancestor at the grouping level." },
            new() { Value = Ancestor, Label = "Ancestor", Description = "Group an intended subtree at a fixed ancestor member." },
            new() { Value = Depth, Label = "Depth", Description = "Resolve to an ancestor at an explicit closure depth." }
        ]
    };
}
