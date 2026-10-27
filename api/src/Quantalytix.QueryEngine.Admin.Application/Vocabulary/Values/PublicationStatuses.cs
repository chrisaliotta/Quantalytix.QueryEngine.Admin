namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>publication_status</c> vocabulary: the lifecycle state of a versioned semantic object.</summary>
public static class PublicationStatuses
{
    /// <summary>Editable metadata under construction; never used by runtime queries.</summary>
    public const string Draft = "draft";

    /// <summary>Passed metadata validation but is not yet runtime-active.</summary>
    public const string Validated = "validated";

    /// <summary>Immutable definition eligible for runtime activation or a publication manifest.</summary>
    public const string Published = "published";

    /// <summary>Historical published definition no longer eligible for new releases.</summary>
    public const string Retired = "retired";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "publication_status",
        Key = "versionStatus",
        Description = "Lifecycle state of a versioned semantic object.",
        Values =
        [
            new() { Value = Draft, Label = "Draft", Description = "Editable metadata under construction; never used by runtime queries." },
            new() { Value = Validated, Label = "Validated", Description = "Passed metadata validation but is not yet runtime-active." },
            new() { Value = Published, Label = "Published", Description = "Immutable definition eligible for runtime activation or a publication manifest." },
            new() { Value = Retired, Label = "Retired", Description = "Historical published definition no longer eligible for new releases." }
        ]
    };
}
