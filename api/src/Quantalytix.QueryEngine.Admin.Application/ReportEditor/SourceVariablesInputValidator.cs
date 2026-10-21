using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Checks a source's complete variable list: each variable once, a variable of the published version of the report's model, and
/// against the active release's version of the source's dataset an enabled measure and a filter over enabled, filterable fields.
/// </summary>
public sealed class SourceVariablesInputValidator : AbstractValidator<SourceVariablesInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="metrics">Reads the variables of the published version of the report's model.</param>
    /// <param name="sources">Reads the route's source.</param>
    /// <param name="releaseDatasets">Reads the active release's version of the source's dataset.</param>
    /// <param name="keys">Names the route's report and source.</param>
    public SourceVariablesInputValidator(
        ModelMetricsReader metrics,
        ITableGateway<ReportSourceRecord> sources,
        ReleaseDatasets releaseDatasets,
        ContentKeys keys)
    {
        RuleForEach(input => input.Variables).ChildRules(variable =>
        {
            variable.RuleFor(input => input.VariableKey).MustBeKey(128);
            variable.RuleFor(input => input.MeasureKey).NotEmpty().MustFitVarchar(128);
            variable.RuleFor(input => input.SourceFilterJson).MustBeFilterOver("field", "the dataset's fields");
        });
        RuleFor(input => input.Variables)
            .Must(list => list.DistinctBy(variable => variable.VariableKey).Count() == list.Count)
            .WithMessage("'{PropertyName}' must list each variable once.");
        RuleFor(input => input.Variables).CustomAsync(async (variables, context, cancellationToken) =>
        {
            var model = await metrics.ReadForReportAsync(keys.ReportId, cancellationToken);
            var source = await sources.GetAsync(new { ReportSourceId = keys.SourceId }, cancellationToken);
            if (await releaseDatasets.ActiveAsync(source.DatasetId, cancellationToken) is not { } dataset)
            {
                context.AddFailure("The source's dataset is not in the active release, which reports run against.");
                return;
            }

            foreach (var variable in variables)
            {
                if (!model.VariableKeys.Contains(variable.VariableKey))
                    context.AddFailure($"'{variable.VariableKey}' is no variable of the published version of the report's model.");
                if (dataset.Measures.GetValueOrDefault(variable.MeasureKey) is not { IsEnabled: true })
                    context.AddFailure($"Variable '{variable.VariableKey}' reads '{variable.MeasureKey}', which is no enabled measure of '{dataset.DatasetKey}'.");
                if (variable.SourceFilterJson is not { } filter) continue;
                foreach (var field in FilterNodes.Inspect(filter).Operands.Select(operand => operand.Id).Distinct(StringComparer.Ordinal)
                    .Where(key => dataset.Fields.GetValueOrDefault(key) is not { IsEnabled: true, IsFilterable: true }))
                    context.AddFailure($"Variable '{variable.VariableKey}' filters on '{field}', which is no enabled, filterable field of '{dataset.DatasetKey}'.");
            }
        });
    }
}
