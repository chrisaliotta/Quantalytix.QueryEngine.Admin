using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Checks a new design source: the attributes every source has, a well-formed key whose bridge key and variable source keys fit
/// their columns and are free, and a bridge to copy mappings from that starts from the source's bound dimension and has a
/// published version.
/// </summary>
public sealed class NewDesignSourceInputValidator : AbstractValidator<NewDesignSourceInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="datasets">Reads the dataset.</param>
    /// <param name="datasetVersions">Reads the dataset's versions.</param>
    /// <param name="bindings">Reads the bindings of the dataset's latest version.</param>
    /// <param name="roles">Reads the role.</param>
    /// <param name="dimensions">Reads the role's dimension.</param>
    /// <param name="designs">Reads the route's design scope.</param>
    /// <param name="sources">Reads the version's variable source keys.</param>
    /// <param name="bridges">Reads the bridge keys and the template bridge.</param>
    /// <param name="bridgeVersions">Reads the template bridge's published version.</param>
    /// <param name="keys">Names the route's report version.</param>
    public NewDesignSourceInputValidator(
        ITableGateway<DatasetRecord> datasets,
        ITableGateway<DatasetVersionRecord> datasetVersions,
        ITableGateway<DimensionBindingRecord> bindings,
        ITableGateway<DimensionRoleRecord> roles,
        ITableGateway<DimensionRecord> dimensions,
        ReportDesignVersions designs,
        ITableGateway<VariableSourceRecord> sources,
        ITableGateway<BridgeRecord> bridges,
        ITableGateway<BridgeVersionRecord> bridgeVersions,
        VersionKeys keys)
    {
        Include(new DesignSourceFieldsValidator(datasets, datasetVersions, bindings, roles, dimensions));
        RuleFor(input => input.Key)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) =>
            {
                var scope = await designs.ReadAsync(keys.VersionId, cancellationToken);
                var bridgeKey = DesignSourceKeys.Bridge(scope.Report.ReportKey, key);
                return bridgeKey.Length <= 128 && (await bridges.ListAsync(new { BridgeKey = bridgeKey }, cancellationToken)).Count == 0;
            })
            .WithMessage(input => $"The source's bridge takes the report's key and '{input.Key}' as its key, which is longer than 128 characters or taken.");
        RuleFor(input => input.Variables).CustomAsync(async (variables, context, cancellationToken) =>
        {
            var taken = (await sources.ListAsync(new { ReportVersionId = keys.VersionId }, cancellationToken))
                .Select(source => source.SourceKey)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var variable in variables)
            {
                var sourceKey = DesignSourceKeys.VariableSource(context.InstanceToValidate.Key, variable.VariableKey);
                if (sourceKey.Length > 128 || taken.Contains(sourceKey))
                    context.AddFailure($"Variable '{variable.VariableKey}' takes source key '{sourceKey}', which is longer than 128 characters or taken in this report version.");
            }
        });
        RuleFor(input => input.CopyMappingsFromBridgeId)
            .MustAsync(async (input, templateId, cancellationToken) =>
            {
                var role = (await roles.ListAsync(new { input.RoleId }, cancellationToken)).SingleOrDefault();
                var template = (await bridges.ListAsync(new { BridgeId = templateId }, cancellationToken)).SingleOrDefault();
                return role is not null && template?.SourceDimensionId == role.DimensionId
                    && (await bridgeVersions.ListAsync(new { BridgeId = templateId, Status = PublicationStatuses.Published }, cancellationToken)).Count == 1;
            })
            .WithMessage(input => $"Bridge {input.CopyMappingsFromBridgeId} is no bridge from the source's bound dimension with a published version.")
            .When(input => input.CopyMappingsFromBridgeId is not null);
    }
}
