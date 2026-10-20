using System.Collections.Frozen;
using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Lookups;

/// <summary>Accepts only known lookup kinds, and requires the parent ID that a child kind lists rows of.</summary>
public sealed class LookupQueryValidator : AbstractValidator<LookupQuery>
{
    /// <summary>The kinds whose rows belong to one parent row.</summary>
    private static readonly FrozenSet<string> ChildKinds = FrozenSet.Create(
        StringComparer.Ordinal,
        LookupKinds.DatasetVersions,
        LookupKinds.Fields,
        LookupKinds.ClassifiedFields,
        LookupKinds.Grains,
        LookupKinds.Bindings,
        LookupKinds.TimeBindings,
        LookupKinds.Measures,
        LookupKinds.DimensionVersions,
        LookupKinds.Levels,
        LookupKinds.Members,
        LookupKinds.MappingVersions,
        LookupKinds.BridgeVersions,
        LookupKinds.ModelVersions,
        LookupKinds.Variables,
        LookupKinds.VariableSources,
        LookupKinds.SourceProfiles,
        LookupKinds.CalculatedMeasures,
        LookupKinds.CalculationDimensions,
        LookupKinds.ReportVersions,
        LookupKinds.ReportColumns);

    /// <summary>Declares the rules.</summary>
    public LookupQueryValidator()
    {
        RuleFor(query => query.Kind).MustBeIn(LookupKinds.Definition);
        RuleFor(query => query.ParentId)
            .NotNull()
            .WithMessage(query => $"'{query.Kind}' lists the rows of one parent, so parentId is required.")
            .When(query => ChildKinds.Contains(query.Kind));
    }
}
