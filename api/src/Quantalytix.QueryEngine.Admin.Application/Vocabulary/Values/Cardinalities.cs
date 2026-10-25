namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>cardinality</c> vocabulary: how many target members a bridge maps each source member to.</summary>
public static class Cardinalities
{
    /// <summary>Each source member maps to at most one target member.</summary>
    public const string ManyToOne = "many_to_one";

    /// <summary>A source member may map to several target members.</summary>
    public const string ManyToMany = "many_to_many";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "cardinality",
        Description = "How many target members a bridge maps each source member to.",
        Values =
        [
            new() { Value = ManyToOne, Description = "Each source member maps to at most one target member." },
            new() { Value = ManyToMany, Description = "A source member may map to several target members." }
        ]
    };
}
