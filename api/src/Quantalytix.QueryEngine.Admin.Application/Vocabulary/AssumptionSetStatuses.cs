namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>assumption_set_status</c> vocabulary: the lifecycle state of an assumption set, which has no validated state.
/// Its values are a subset of <see cref="PublicationStatuses"/>.
/// </summary>
public static class AssumptionSetStatuses
{
    /// <summary>Editable assumption set whose pinned versions may still change.</summary>
    public const string Draft = PublicationStatuses.Draft;

    /// <summary>Immutable snapshot; later dimension changes never mutate it.</summary>
    public const string Published = PublicationStatuses.Published;

    /// <summary>Historical assumption set no longer offered for new reporting.</summary>
    public const string Retired = PublicationStatuses.Retired;

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "assumption_set_status",
        Description = "Lifecycle state of an assumption set, which has no validated state.",
        Values =
        [
            new() { Value = Draft, Description = "Editable assumption set whose pinned versions may still change." },
            new() { Value = Published, Description = "Immutable snapshot; later dimension changes never mutate it." },
            new() { Value = Retired, Description = "Historical assumption set no longer offered for new reporting." }
        ]
    };
}
