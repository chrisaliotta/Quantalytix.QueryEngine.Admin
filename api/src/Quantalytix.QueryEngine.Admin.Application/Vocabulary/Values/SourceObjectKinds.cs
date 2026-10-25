namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>source_object_kind</c> vocabulary: the kind of physical object a dataset version queries.</summary>
public static class SourceObjectKinds
{
    /// <summary>Query a tenant-local table.</summary>
    public const string Table = "table";

    /// <summary>Query an allowlisted managed view.</summary>
    public const string View = "view";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "source_object_kind",
        Description = "Kind of physical object a dataset version queries.",
        Values =
        [
            new() { Value = Table, Description = "Query a tenant-local table." },
            new() { Value = View, Description = "Query an allowlisted managed view." }
        ]
    };
}
