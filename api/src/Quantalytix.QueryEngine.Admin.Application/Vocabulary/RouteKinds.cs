namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>route_kind</c> vocabulary: how a calculation dimension route produces its dimension. A scalar calculation dimension's
/// routes read raw values; a canonical one's reuse their field's dataset binding, or classify rows like a source, with the
/// <see cref="ClassificationKinds"/> values.
/// </summary>
public static class RouteKinds
{
    /// <summary>The field's raw values.</summary>
    public const string Field = "field";

    /// <summary>The field's own dataset binding, grouped as the calculation dimension says.</summary>
    public const string Binding = "binding";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "route_kind",
        Description = "How a calculation dimension route produces its dimension.",
        Values =
        [
            new() { Value = Field, Description = "The field's raw values, for a scalar calculation dimension." },
            new() { Value = Binding, Description = "The field's own dataset binding, which groups members of the calculation dimension's dimension; the calculation dimension's grouping applies on top." },
            new() { Value = ClassificationKinds.Direct, Description = "The field holds member keys of the calculation dimension's dimension." },
            new() { Value = ClassificationKinds.Mapped, Description = "A mapping set maps the field's codes to members of the calculation dimension's dimension." },
            new() { Value = ClassificationKinds.Bridge, Description = "A bridge remaps the members the field's binding classifies rows into to members of the calculation dimension's dimension." }
        ]
    };
}
