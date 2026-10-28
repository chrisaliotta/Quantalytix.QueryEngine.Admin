using Quantalytix.QueryEngine.Admin.Application.Dimensions;

namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// The <c>edit_policy</c> vocabulary over <see cref="DimensionEditPolicies"/>: how far a dimension profile lets users edit a
/// profiled dimension. Served by the tenant module until the dimensions module declares it.
/// </summary>
public static class EditPolicies
{
    /// <summary>The vocabulary's definition, as the web client reads it.</summary>
    public static readonly Vocabulary.Vocabulary Definition = new()
    {
        Name = "edit_policy",
        Key = "editPolicy",
        Description = "How far a dimension profile lets users edit a profiled dimension.",
        Values =
        [
            new() { Value = DimensionEditPolicies.Open, Label = "Open", Description = "Users edit members freely." },
            new() { Value = DimensionEditPolicies.Extend, Label = "Extend", Description = "Users add members; managed members are fixed." },
            new() { Value = DimensionEditPolicies.Locked, Label = "Locked", Description = "Read-only; managed by its profile." }
        ]
    };
}
