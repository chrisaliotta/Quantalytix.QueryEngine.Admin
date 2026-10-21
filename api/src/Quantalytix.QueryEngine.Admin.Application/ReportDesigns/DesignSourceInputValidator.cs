using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Checks an edit of a design source: the attributes every source has, a route bridge that is a uniform source of the version,
/// because only then can one dataset, role, time field and filter replace its sources', a role of the dimension the bridge maps
/// from, which its bridge fixes, and free variable source keys for the variables the edit adds.
/// </summary>
public sealed class DesignSourceInputValidator : AbstractValidator<DesignSourceInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="datasets">Reads the dataset.</param>
    /// <param name="datasetVersions">Reads the dataset's versions.</param>
    /// <param name="bindings">Reads the bindings of the dataset's latest version.</param>
    /// <param name="roles">Reads the role.</param>
    /// <param name="dimensions">Reads the role's dimension.</param>
    /// <param name="designs">Reads the route's design scope.</param>
    /// <param name="sources">Reads the version's variable sources.</param>
    /// <param name="keys">Names the route's report version.</param>
    /// <param name="route">Names the source's bridge.</param>
    public DesignSourceInputValidator(
        ITableGateway<DatasetRecord> datasets,
        ITableGateway<DatasetVersionRecord> datasetVersions,
        ITableGateway<DimensionBindingRecord> bindings,
        ITableGateway<DimensionRoleRecord> roles,
        ITableGateway<DimensionRecord> dimensions,
        ReportDesignVersions designs,
        ITableGateway<VariableSourceRecord> sources,
        VersionKeys keys,
        IRequestRoute route)
    {
        Include(new DesignSourceFieldsValidator(datasets, datasetVersions, bindings, roles, dimensions));
        RuleFor(input => input.RoleId)
            .MustAsync(async (roleId, cancellationToken) =>
            {
                var scope = await designs.ReadAsync(keys.VersionId, cancellationToken);
                if (scope.Bridges.GetValueOrDefault(route.Id("bridgeId")) is not { } bridge) return true;
                return (await roles.ListAsync(new { RoleId = roleId, DimensionId = bridge.SourceDimensionId }, cancellationToken)).Count == 1;
            })
            .WithMessage("The source's bridge maps from the dimension of the role it was created with; a role of another dimension needs a new source.");
        RuleFor(input => input.Variables).CustomAsync(async (variables, context, cancellationToken) =>
        {
            var bridgeId = route.Id("bridgeId");
            var versionSources = await sources.ListAsync(new { ReportVersionId = keys.VersionId }, cancellationToken);
            var own = versionSources.Where(source => source.BridgeId == bridgeId).ToList();
            if (own.Count == 0)
            {
                context.AddFailure($"Bridge {bridgeId} is no source of this report version.");
                return;
            }
            if (!DesignSourceShape.IsUniform(own))
            {
                context.AddFailure("The source's variable sources read different datasets, roles, time fields or filters; edit them one by one in the Sources tab.");
                return;
            }

            var scope = await designs.ReadAsync(keys.VersionId, cancellationToken);
            var sourceKey = DesignSourceKeys.Source(scope.Report.ReportKey, scope.Bridges[bridgeId].BridgeKey);
            var ownVariables = own.Select(source => source.VariableKey).ToHashSet(StringComparer.Ordinal);
            var taken = versionSources.Select(source => source.SourceKey).ToHashSet(StringComparer.Ordinal);
            foreach (var variable in variables.Where(variable => !ownVariables.Contains(variable.VariableKey)))
            {
                var variableSourceKey = DesignSourceKeys.VariableSource(sourceKey, variable.VariableKey);
                if (variableSourceKey.Length > 128 || taken.Contains(variableSourceKey))
                    context.AddFailure($"Variable '{variable.VariableKey}' takes source key '{variableSourceKey}', which is longer than 128 characters or taken in this report version.");
            }
        });
    }
}
