namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>row_role</c> vocabulary: what a report row is to its reader, which picks the style a row without one of its own shows.</summary>
public static class RowRoles
{
    /// <summary>A section, or a member whose values are hidden.</summary>
    public const string Header = "header";

    /// <summary>A member with members under it.</summary>
    public const string Subtotal = "subtotal";

    /// <summary>The report dimension's root member.</summary>
    public const string Total = "total";

    /// <summary>A member without members under it.</summary>
    public const string Detail = "detail";

    /// <summary>A formula row.</summary>
    public const string Formula = "formula";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "row_role",
        Description = "What a report row is to its reader, which picks the style a row without one of its own shows.",
        Values =
        [
            new() { Value = Header, Description = "A section, or a member whose values are hidden." },
            new() { Value = Subtotal, Description = "A member with members under it." },
            new() { Value = Total, Description = "The report dimension's root member." },
            new() { Value = Detail, Description = "A member without members under it." },
            new() { Value = Formula, Description = "A formula row." }
        ]
    };
}
