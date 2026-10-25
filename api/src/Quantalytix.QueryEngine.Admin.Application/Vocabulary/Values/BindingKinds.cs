namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>binding_kind</c> vocabulary: how a dimension binding classifies its source value.</summary>
public static class BindingKinds
{
    /// <summary>The source value already is the canonical member key; no mapping table is required.</summary>
    public const string Direct = "direct";

    /// <summary>The source key resolves through the mapping version of the mapping set and resolved dimension version.</summary>
    public const string Mapped = "mapped";

    /// <summary>The scalar source value is assigned through the range bands of the resolved dimension version.</summary>
    public const string Range = "range";

    /// <summary>An already canonical source binding is remapped through a bridge, in the version the assumption set pins.</summary>
    public const string Bridge = "bridge";

    /// <summary>The source field defines a logical time axis and has a time binding extension.</summary>
    public const string Time = "time";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "binding_kind",
        Description = "How a dimension binding classifies its source value.",
        Values =
        [
            new() { Value = Direct, Description = "The source value already is the canonical member key; no mapping table is required." },
            new() { Value = Mapped, Description = "The source key resolves through the mapping set, in the version the assumption set pins." },
            new() { Value = Range, Description = "The scalar source value is assigned through the range bands of the resolved dimension version." },
            new() { Value = Bridge, Description = "An already canonical source binding is remapped through a bridge, in the version the assumption set pins." },
            new() { Value = Time, Description = "The source field defines a logical time axis and has a time binding extension." }
        ]
    };
}
