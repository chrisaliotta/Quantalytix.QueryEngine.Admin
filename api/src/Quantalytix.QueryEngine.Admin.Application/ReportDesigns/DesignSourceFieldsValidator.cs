using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Checks the attributes every design source has: a label, a dataset that exists, a role of an analytical dimension that the
/// dataset's latest version binds into members, as a bridge from it needs, a time field, a filter over the dataset's fields, and
/// at least one variable, each once with its measure. Whether the keys resolve in the dataset version a release pins is a release
/// rule. Internal, so the create and edit validators that include it are the only ones registered.
/// </summary>
internal sealed class DesignSourceFieldsValidator : AbstractValidator<DesignSourceInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="datasets">Reads the dataset.</param>
    /// <param name="datasetVersions">Reads the dataset's versions.</param>
    /// <param name="bindings">Reads the bindings of the dataset's latest version.</param>
    /// <param name="roles">Reads the role.</param>
    /// <param name="dimensions">Reads the role's dimension.</param>
    public DesignSourceFieldsValidator(
        ITableGateway<DatasetRecord> datasets,
        ITableGateway<DatasetVersionRecord> datasetVersions,
        ITableGateway<DimensionBindingRecord> bindings,
        ITableGateway<DimensionRoleRecord> roles,
        ITableGateway<DimensionRecord> dimensions)
    {
        RuleFor(input => input.Label).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.DatasetId)
            .MustAsync(async (datasetId, cancellationToken) => (await datasets.ListAsync(new { DatasetId = datasetId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dataset {input.DatasetId} does not exist.");
        RuleFor(input => input.RoleId)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (roleId, cancellationToken) =>
            {
                var role = (await roles.ListAsync(new { RoleId = roleId }, cancellationToken)).SingleOrDefault();
                return role is not null
                    && (await dimensions.GetAsync(new { role.DimensionId }, cancellationToken)).DimensionPurpose == DimensionPurposes.Analytical;
            })
            .WithMessage(input => $"Role {input.RoleId} is no role of an analytical dimension.")
            .MustAsync(async (input, roleId, cancellationToken) =>
            {
                var latest = (await datasetVersions.ListAsync(new { input.DatasetId }, cancellationToken)).MaxBy(version => version.VersionNumber);
                if (latest is null) return false;
                var bound = await bindings.ListAsync(new { latest.DatasetVersionId, RoleId = roleId }, cancellationToken);
                return bound.Any(ClassificationFields.FeedsBridges);
            })
            .WithMessage(input => $"The latest version of dataset {input.DatasetId} binds role {input.RoleId} into no members with an enabled binding.");
        RuleFor(input => input.TimeFieldKey).NotEmpty().MustFitVarchar(128);
        RuleFor(input => input.SourceFilterJson).MustBeFilterOver("field", "the dataset's fields");
        RuleFor(input => input.Variables).NotEmpty().WithMessage("A source supplies at least one variable.");
        RuleForEach(input => input.Variables).ChildRules(variable =>
        {
            variable.RuleFor(input => input.VariableKey).MustBeKey(128);
            variable.RuleFor(input => input.MeasureKey).NotEmpty().MustFitVarchar(128);
        });
        RuleFor(input => input.Variables).Custom((variables, context) =>
        {
            foreach (var repeated in variables.GroupBy(variable => variable.VariableKey, StringComparer.Ordinal).Where(group => group.Count() > 1))
                context.AddFailure($"Variable '{repeated.Key}' appears more than once.");
        });
    }
}
