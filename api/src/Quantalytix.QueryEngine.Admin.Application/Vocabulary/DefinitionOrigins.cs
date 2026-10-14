namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>definition_origin</c> vocabulary: who defined a dimension.</summary>
public static class DefinitionOrigins
{
    /// <summary>Platform fixture.</summary>
    public const string System = "system";

    /// <summary>Tenant or user-defined dimension.</summary>
    public const string User = "user";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "definition_origin",
        Description = "Who defined a dimension.",
        Values =
        [
            new() { Value = System, Description = "Platform fixture." },
            new() { Value = User, Description = "Tenant or user-defined dimension." }
        ]
    };
}
