namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>classification_kind</c> vocabulary: how a report source's rows reach the members of the report's dimension.</summary>
public static class ClassificationKinds
{
    /// <summary>The field holds member keys.</summary>
    public const string Direct = "direct";

    /// <summary>A mapping set maps the field's source codes to members.</summary>
    public const string Mapped = "mapped";

    /// <summary>A bridge remaps the members the field's own binding classifies rows into.</summary>
    public const string Bridge = "bridge";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "classification_kind",
        Description = "How a report source's rows reach the members of the report's dimension.",
        Values =
        [
            new() { Value = Direct, Description = "The field holds member keys of the report's dimension." },
            new() { Value = Mapped, Description = "A mapping set maps the field's source codes to members of the report's dimension." },
            new() { Value = Bridge, Description = "A bridge remaps the members the field's binding classifies rows into to members of the report's dimension." }
        ]
    };
}
