using System.Text.Json;
using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks each report operation's input shape before it applies: keys, names and numbers that fit their columns, vocabulary values,
/// well-formed filters, a formula row's references and a source's method-specific fields, and lists that name each item once. What
/// the input means for the report (a taken key, a row that is no data row) is decided as the operation applies, and what it means
/// for the model, the release and the role dimensions by the report rules.
/// </summary>
public sealed class ReportOperationValidator : AbstractValidator<ReportOperation>
{
    /// <summary>The keys a report table's schema uses before its columns.</summary>
    private static readonly string[] ReservedColumnKeys = ["line", "period"];

    /// <summary>A <c>#RRGGBB</c> color, matching the whole text.</summary>
    private const string ColorPattern = @"^#[0-9A-Fa-f]{6}\z";

    /// <summary>Declares the rules.</summary>
    public ReportOperationValidator()
    {
        RuleFor(operation => operation).SetInheritanceValidator(operations =>
        {
            operations.Add(new InlineValidator<ReportUpdateSettingsOperation>
            {
                validator => validator.RuleFor(operation => operation.Patch).Must(patch => !patch.IsEmpty).WithMessage("Name at least one setting to change."),
                validator => validator.RuleFor(operation => operation.Patch).SetValidator(new PatchValidator())
            });
            operations.Add(new InlineValidator<ReportSaveRowOperation>
            {
                validator => validator.RuleFor(operation => operation.Row).SetValidator(new RowValidator()),
                validator => validator.RuleFor(operation => operation.Index).GreaterThanOrEqualTo(0)
            });
            operations.Add(new InlineValidator<ReportMoveRowOperation>
            {
                validator => validator.RuleFor(operation => operation.Key).NotEmpty(),
                validator => validator.RuleFor(operation => operation.To.Index).GreaterThanOrEqualTo(0)
            });
            operations.Add(new InlineValidator<ReportDeleteRowOperation> { validator => validator.RuleFor(operation => operation.Key).NotEmpty() });
            operations.Add(new InlineValidator<ReportSaveSourceOperation> { validator => validator.RuleFor(operation => operation.Input).SetValidator(new SourceValidator()) });
            operations.Add(new InlineValidator<ReportDeleteSourceOperation> { validator => validator.RuleFor(operation => operation.Key).NotEmpty() });
            operations.Add(new InlineValidator<ReportSetSourceVariablesOperation>
            {
                validator => validator.RuleFor(operation => operation.SourceKey).NotEmpty(),
                validator => validator.RuleForEach(operation => operation.Variables).SetValidator(new SourceVariableValidator()),
                validator => validator.RuleFor(operation => operation.Variables).Must(list => list.DistinctBy(variable => variable.VariableKey).Count() == list.Count).WithMessage("'{PropertyName}' must list each variable once.")
            });
            operations.Add(new InlineValidator<ReportSetRowRulesOperation>
            {
                validator => validator.RuleFor(operation => operation.SourceKey).NotEmpty(),
                validator => validator.RuleFor(operation => operation.RowKey).NotEmpty(),
                validator => validator.RuleForEach(operation => operation.Rules).ChildRules(rule =>
                {
                    rule.RuleFor(input => input.ItemKey).MustBeMemberKey();
                    rule.RuleFor(input => input.Selection).MustBeIn(SourceSelections.Definition);
                    rule.RuleFor(input => input.Factor).MustFitDecimal();
                }),
                validator => validator.RuleFor(operation => operation.Rules).Must(list => list.DistinctBy(rule => rule.ItemKey).Count() == list.Count).WithMessage("'{PropertyName}' must list each item once.")
            });
            operations.Add(new InlineValidator<ReportSetLeftOutOperation>
            {
                validator => validator.RuleFor(operation => operation.SourceKey).NotEmpty(),
                validator => validator.RuleForEach(operation => operation.ItemKeys).MustBeMemberKey(),
                validator => validator.RuleFor(operation => operation.ItemKeys).Must(list => list.Distinct(StringComparer.Ordinal).Count() == list.Count).WithMessage("'{PropertyName}' must list each item once.")
            });
            operations.Add(new InlineValidator<ReportSaveProfileOperation>
            {
                validator => validator.RuleFor(operation => operation.Profile.Key).MustBeKey(128),
                validator => validator.RuleFor(operation => operation.Profile.DisplayName).NotEmpty().MustFitVarchar(256)
            });
            operations.Add(new InlineValidator<ReportDeleteProfileOperation> { validator => validator.RuleFor(operation => operation.Key).NotEmpty() });
            operations.Add(new InlineValidator<ReportSaveCalculationDimensionOperation>
            {
                validator => validator.RuleFor(operation => operation.Input.Key).MustBeKey(128),
                validator => validator.RuleFor(operation => operation.Input.DisplayName).NotEmpty().MustFitVarchar(256),
                validator => validator.RuleFor(operation => operation.Input.Grouping).SetValidator(new GroupingValidator())
            });
            operations.Add(new InlineValidator<ReportDeleteCalculationDimensionOperation> { validator => validator.RuleFor(operation => operation.Key).NotEmpty() });
            operations.Add(new InlineValidator<ReportSetScenariosOperation>
            {
                validator => validator.RuleFor(operation => operation.RoleIds).Must(list => list.Distinct().Count() == list.Count).WithMessage("'{PropertyName}' must name each role once.")
            });
            operations.Add(new InlineValidator<ReportSetVariableScopesOperation>
            {
                validator => validator.RuleForEach(operation => operation.Scopes).ChildRules(scope =>
                {
                    scope.RuleFor(input => input.VariableKey).MustBeKey(128);
                    scope.RuleFor(input => input.ScopeMemberKey).NotEmpty().MustFitVarchar(128);
                    scope.RuleFor(input => input.SelectionScope)
                        .Must(value => value is SelectionScopes.Exact or SelectionScopes.Descendants)
                        .WithMessage("A variable without a scope reads every row it classifies, so a scope's '{PropertyName}' is exact or descendants.");
                }),
                validator => validator.RuleFor(operation => operation.Scopes).Must(list => list.DistinctBy(scope => scope.VariableKey).Count() == list.Count).WithMessage("'{PropertyName}' must scope each variable once.")
            });
            operations.Add(new InlineValidator<ReportSetColumnsOperation>
            {
                validator => validator.RuleForEach(operation => operation.Columns).ChildRules(column =>
                {
                    column.RuleFor(input => input.Key)
                        .MustBeKey(128)
                        .Must(key => !ReservedColumnKeys.Contains(key))
                        .WithMessage(input => $"'{input.Key}' names a report table's own column; choose another key.");
                    column.RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256).When(input => input.DisplayName is not null);
                    column.RuleFor(input => input.ValueKind).MustBeIn(ReportValueKinds.Definition);
                    column.RuleFor(input => input.MetricKey).NotEmpty().MustFitVarchar(128);
                    column.RuleFor(input => input.DisplayFormat).NotEmpty().MustFitVarchar(32).When(input => input.DisplayFormat is not null);
                }),
                validator => validator.RuleFor(operation => operation.Columns).Must(list => list.DistinctBy(column => column.Key).Count() == list.Count).WithMessage("'{PropertyName}' must give each column a different key.")
            });
            operations.Add(new InlineValidator<ReportSaveStyleOperation> { validator => validator.RuleFor(operation => operation.Style).SetValidator(new StyleValidator()) });
            operations.Add(new InlineValidator<ReportDeleteStyleOperation> { validator => validator.RuleFor(operation => operation.Key).NotEmpty() });
        });
    }

    /// <summary>Checks a settings patch.</summary>
    private sealed class PatchValidator : AbstractValidator<ReportSettingsPatch>
    {
        /// <summary>Declares the rules.</summary>
        public PatchValidator()
        {
            RuleFor(patch => patch.DisplayName).NotEmpty().MustFitVarchar(256).When(patch => patch.DisplayName is not null);
            RuleFor(patch => patch.ReportType).NotEmpty().MustFitVarchar(32).When(patch => patch.ReportType is not null);
            RuleFor(patch => patch.SubtotalPosition).MustBeIn(SubtotalPositions.Definition);
            RuleFor(patch => patch.DefaultProfileKey).MustFitVarchar(128);
            RuleFor(patch => patch.Periodicity).MustBeIn(Periodicities.Definition);
            RuleFor(patch => patch.Layout).MustBeIn(ReportLayouts.Definition);
            RuleFor(patch => patch.DefaultBreakout!).Must(list => list.Distinct(StringComparer.Ordinal).Count() == list.Count).WithMessage("'{PropertyName}' must name each calculation dimension once.")
                .When(patch => patch.DefaultBreakout is not null);
            RuleFor(patch => patch.DefaultFilter)
                .MustBeFilterNodeOver("dimension", "calculation dimensions and request parameters", readsParameters: true);
        }
    }

    /// <summary>Checks a row's fields; the meaning of its keys is decided as it applies.</summary>
    private sealed class RowValidator : AbstractValidator<ReportRow>
    {
        /// <summary>Declares the rules.</summary>
        public RowValidator()
        {
            RuleFor(row => row.Key).NotEmpty().MustFitVarchar(128);
            RuleFor(row => row.Label).NotEmpty().MustFitVarchar(256);
            RuleFor(row => row.Kind).MustBeIn(ReportRowKinds.Definition);
            RuleFor(row => row.ShowsValues).Equal(true).When(row => row.Kind != ReportRowKinds.Group)
                .WithMessage("Only a group row can hide its total and head the rows under it, so '{PropertyName}' must be true.");
            RuleFor(row => row.CalculatedMeasureKey).NotEmpty().When(row => row.Kind == ReportRowKinds.Formula);
            RuleFor(row => row.ValueColumnKey).NotEmpty().When(row => row.Kind == ReportRowKinds.Formula);
            RuleFor(row => row.CalculatedMeasureKey).Null().When(row => row.Kind != ReportRowKinds.Formula)
                .WithMessage("Only formula rows evaluate a calculated measure, so '{PropertyName}' must be empty.");
            RuleFor(row => row.ValueColumnKey).Null().When(row => row.Kind != ReportRowKinds.Formula)
                .WithMessage("Only formula rows place a value in a column, so '{PropertyName}' must be empty.");
            RuleFor(row => row.StyleKey).MustFitVarchar(128);
            RuleForEach(row => row.Parameters).Must(value => ValueFits(value.Value))
                .WithMessage("A row parameter value is a number that fits DECIMAL(38, 12) or a text of at most 256 bytes.");
        }

        /// <summary>Whether a parameter value is a number or text its column holds.</summary>
        private static bool ValueFits(JsonElement value)
        {
            var (number, text) = RowParameterJson.Read(value);
            return number is { } decimalValue ? DecimalRuleExtensions.Fits(decimalValue, 38, 12) : text is not null && VarcharRuleExtensions.Fits(text, 256);
        }
    }

    /// <summary>Checks a source's identity: a bridge source names a role and no field, a direct source a field and no role.</summary>
    private sealed class SourceValidator : AbstractValidator<ReportSourceInput>
    {
        /// <summary>Declares the rules.</summary>
        public SourceValidator()
        {
            RuleFor(input => input.Key).MustBeKey(128);
            RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
            RuleFor(input => input.TimeFieldKey).NotEmpty().MustFitVarchar(128);
            RuleFor(input => input.Method).MustBeIn(ReportMappingMethods.Definition);
            When(input => input.Method == ReportMappingMethods.Bridge, () =>
            {
                RuleFor(input => input.RoleId).NotNull().WithMessage("A bridge source starts from a role the dataset binds, so '{PropertyName}' is required.");
                RuleFor(input => input.FieldKey).Null().WithMessage("A bridge source maps rows through its bridge, so '{PropertyName}' must be empty.");
            });
            When(input => input.Method == ReportMappingMethods.Direct, () =>
            {
                RuleFor(input => input.FieldKey).NotEmpty().MustFitVarchar(128).WithMessage("A direct source reads row keys from a field, so '{PropertyName}' is required.");
                RuleFor(input => input.RoleId).Null().WithMessage("A direct source reads row keys from a field, so '{PropertyName}' must be empty.");
            });
        }
    }

    /// <summary>Checks a source variable: keys, a filter over fields, and a missing-behavior override.</summary>
    private sealed class SourceVariableValidator : AbstractValidator<ReportSourceVariable>
    {
        /// <summary>Declares the rules.</summary>
        public SourceVariableValidator()
        {
            RuleFor(variable => variable.VariableKey).MustBeKey(128);
            RuleFor(variable => variable.MeasureKey).NotEmpty().MustFitVarchar(128);
            RuleFor(variable => variable.Filter).MustBeFilterNodeOver("field", "the dataset's fields");
            RuleFor(variable => variable.MissingBehavior).MustBeIn(VariableMissingBehaviors.Definition);
        }
    }

    /// <summary>Checks a grouping's keys and depth.</summary>
    private sealed class GroupingValidator : AbstractValidator<Grouping>
    {
        /// <summary>Declares the rules.</summary>
        public GroupingValidator()
        {
            RuleFor(grouping => grouping).SetInheritanceValidator(groupings =>
            {
                groupings.Add(new InlineValidator<GroupingMember>());
                groupings.Add(new InlineValidator<GroupingLevel> { validator => validator.RuleFor(grouping => grouping.LevelKey).NotEmpty().MustFitVarchar(128) });
                groupings.Add(new InlineValidator<GroupingAncestor> { validator => validator.RuleFor(grouping => grouping.MemberKey).NotEmpty().MustFitVarchar(128) });
                groupings.Add(new InlineValidator<GroupingDepth> { validator => validator.RuleFor(grouping => grouping.Depth).GreaterThanOrEqualTo(0) });
            });
        }
    }

    /// <summary>Checks a style's fields.</summary>
    private sealed class StyleValidator : AbstractValidator<ReportStyle>
    {
        /// <summary>Declares the rules.</summary>
        public StyleValidator()
        {
            RuleFor(style => style.Key).MustBeKey(128);
            RuleFor(style => style.DisplayName).NotEmpty().MustFitVarchar(256);
            RuleFor(style => style.DefaultRole).MustBeIn(RowRoles.Definition);
            RuleFor(style => style.FontFamily).NotEmpty().MustFitVarchar(128).When(style => style.FontFamily is not null);
            RuleFor(style => style.FontSize).InclusiveBetween(6m, 72m).PrecisionScale(5, 2, true);
            RuleFor(style => style.FontWeight).MustBeIn(FontWeights.Definition);
            RuleFor(style => style.TextColor).Matches(ColorPattern).WithMessage("'{PropertyName}' must be a color such as '#1F2937'.");
            RuleFor(style => style.BackgroundColor).Matches(ColorPattern).WithMessage("'{PropertyName}' must be a color such as '#F3F4F6'.");
            RuleFor(style => style.BorderTop).MustBeIn(BorderStyles.Definition);
            RuleFor(style => style.BorderBottom).MustBeIn(BorderStyles.Definition);
            RuleFor(style => style.Indent).InclusiveBetween(0, 10);
        }
    }
}
