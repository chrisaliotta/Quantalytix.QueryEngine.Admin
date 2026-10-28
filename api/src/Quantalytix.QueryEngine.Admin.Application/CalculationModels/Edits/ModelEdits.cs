using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Applies the model operations to a model's content in memory. Items are named by key: a save without <c>originalKey</c> creates an
/// item whose key no other item of its namespace has (variables and calculated measures share one, C1), and a save or delete of a
/// stored item names a key the model has; keys never change (C5, C9). Deleting a metric deletes its attribution with it. Whether the
/// result still holds together is the gate's concern, because every operation can break or repair what another reads.
/// </summary>
public static class ModelEdits
{
    /// <summary>What a new metric's key must be free among (C1).</summary>
    private const string MetricNamespace = "variable or calculated measure";

    /// <summary>Applies one operation.</summary>
    /// <param name="content">The model as the earlier operations left it.</param>
    /// <param name="operation">The operation, whose input its validator has passed.</param>
    /// <param name="context">Allocates the ids of new rows and takes the refusals.</param>
    /// <returns>The model with the operation applied, or as it was when refused.</returns>
    public static ModelGraph Apply(ModelGraph content, ModelOperation operation, ChangeContext context)
    {
        return operation switch
        {
            ModelUpdateModelOperation update => content with { Model = Patched(content.Model, update.Patch) },
            ModelSaveVariableOperation save => SaveVariable(content, save, context),
            ModelDeleteVariableOperation delete => Has(content.VariableKeys, delete.Key, "key", "variable", context)
                ? WithoutAttribution(content with { Variables = [.. content.Variables.Where(variable => variable.VariableKey != delete.Key)] }, delete.Key)
                : content,
            ModelSaveMeasureOperation save => SaveMeasure(content, save, context),
            ModelDeleteMeasureOperation delete => Has(MeasureKeys(content), delete.Key, "key", "calculated measure", context)
                ? WithoutAttribution(content with { CalculatedMeasures = [.. content.CalculatedMeasures.Where(measure => measure.MeasureKey != delete.Key)] }, delete.Key)
                : content,
            ModelSaveParameterOperation save => SaveParameter(content, save, context),
            ModelDeleteParameterOperation delete => Has(ParameterKeys(content), delete.Key, "key", "parameter", context)
                ? content with { Parameters = [.. content.Parameters.Where(parameter => parameter.ParameterKey != delete.Key)] }
                : content,
            ModelSetAttributionOperation set => content with { Attributions = [.. WithoutAttribution(content, set.Input.MetricKey).Attributions, set.Input] },
            ModelClearAttributionOperation clear => Has(AttributedKeys(content), clear.MetricKey, "metricKey", "attribution of a metric", context)
                ? WithoutAttribution(content, clear.MetricKey)
                : content,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Not a model operation.")
        };
    }

    /// <summary>The model row with a patch's settings.</summary>
    private static CalculationModelRecord Patched(CalculationModelRecord model, CalcModelPatch patch)
    {
        return model with
        {
            DisplayName = patch.DisplayName ?? model.DisplayName,
            CurrencyCode = patch.CurrencyCode ?? model.CurrencyCode,
            CalendarKey = patch.CalendarKey ?? model.CalendarKey,
            FormulaLanguageVersion = patch.FormulaLanguage ?? model.FormulaLanguageVersion,
            AttributionMethod = patch.AttributionMethod ?? model.AttributionMethod,
            IsEnabled = patch.IsEnabled ?? model.IsEnabled
        };
    }

    /// <summary>Creates or edits a variable; an edit keeps the stored row's id.</summary>
    private static ModelGraph SaveVariable(ModelGraph content, ModelSaveVariableOperation save, ChangeContext context)
    {
        if (!Saves(content.MetricKeys, MetricNamespace, content.VariableKeys, save, save.Input.Key, "variable", context)) return content;
        var stored = content.Variables.FirstOrDefault(variable => variable.VariableKey == save.Input.Key);
        var row = new VariableRecord
        {
            VariableId = stored?.VariableId ?? context.Ids.NewId(),
            CalculationModelId = content.Model.CalculationModelId,
            VariableKey = save.Input.Key,
            DisplayName = save.Input.DisplayName,
            MissingBehavior = save.Input.MissingBehavior,
            DisplayFormat = save.Input.DisplayFormat,
            UnitKey = save.Input.UnitKey
        };
        return content with { Variables = [.. content.Variables.Where(variable => variable.VariableKey != row.VariableKey), row] };
    }

    /// <summary>Creates or edits a calculated measure; an edit keeps the stored row's id.</summary>
    private static ModelGraph SaveMeasure(ModelGraph content, ModelSaveMeasureOperation save, ChangeContext context)
    {
        if (!Saves(content.MetricKeys, MetricNamespace, MeasureKeys(content), save, save.Input.Key, "calculated measure", context)) return content;
        var stored = content.CalculatedMeasures.FirstOrDefault(measure => measure.MeasureKey == save.Input.Key);
        var row = new CalculatedMeasureRecord
        {
            CalculatedMeasureId = stored?.CalculatedMeasureId ?? context.Ids.NewId(),
            CalculationModelId = content.Model.CalculationModelId,
            MeasureKey = save.Input.Key,
            DisplayName = save.Input.DisplayName,
            ExpressionText = save.Input.Formula,
            OutputDataType = save.Input.OutputDataType,
            OutputPrecision = save.Input.OutputPrecision,
            OutputScale = save.Input.OutputScale,
            RoundingMode = save.Input.RoundingMode,
            UnitKey = save.Input.UnitKey,
            DisplayFormat = save.Input.DisplayFormat
        };
        return content with { CalculatedMeasures = [.. content.CalculatedMeasures.Where(measure => measure.MeasureKey != row.MeasureKey), row] };
    }

    /// <summary>Creates or edits a parameter.</summary>
    private static ModelGraph SaveParameter(ModelGraph content, ModelSaveParameterOperation save, ChangeContext context)
    {
        var keys = ParameterKeys(content);
        if (!Saves(keys, "parameter", keys, save, save.Input.Key, "parameter", context)) return content;
        var parameter = new ModelParameter
        {
            ParameterKey = save.Input.Key,
            DisplayName = save.Input.DisplayName,
            ValueType = save.Input.ValueType,
            AllowedValues = save.Input.AllowedValues,
            DefaultNumber = save.Input.DefaultNumber,
            DefaultText = save.Input.DefaultText,
            Description = save.Input.Description
        };
        return content with { Parameters = [.. content.Parameters.Where(existing => existing.ParameterKey != parameter.ParameterKey), parameter] };
    }

    /// <summary>
    /// Whether a save applies: a new item needs a key free in its namespace; an edit names a stored item of its kind and keeps its
    /// key. Refuses the operation otherwise.
    /// </summary>
    private static bool Saves(
        IReadOnlySet<string> taken, string takenBy, IReadOnlySet<string> kind, ModelSaveOperation save, string key, string noun, ChangeContext context)
    {
        if (save.OriginalKey is null)
        {
            if (taken.Contains(key)) context.Reject("input.key", $"The model already has a {takenBy} with key '{key}'.");
        }
        else if (Has(kind, save.OriginalKey, "originalKey", noun, context) && key != save.OriginalKey)
            context.Reject("input.key", $"The {noun} '{save.OriginalKey}' keeps its key; keys are fixed once saved.");

        return !context.Refused;
    }

    /// <summary>Whether the model has an item with a key; refuses the operation at the path otherwise.</summary>
    private static bool Has(IReadOnlySet<string> keys, string key, string path, string noun, ChangeContext context)
    {
        if (!keys.Contains(key)) context.Reject(path, $"The model has no {noun} '{key}'.");
        return !context.Refused;
    }

    /// <summary>The model without a metric's attribution.</summary>
    private static ModelGraph WithoutAttribution(ModelGraph content, string metricKey)
    {
        return content with { Attributions = [.. content.Attributions.Where(attribution => attribution.MetricKey != metricKey)] };
    }

    /// <summary>The model's calculated measure keys.</summary>
    private static HashSet<string> MeasureKeys(ModelGraph content)
    {
        return content.CalculatedMeasures.Select(measure => measure.MeasureKey).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The model's parameter keys.</summary>
    private static HashSet<string> ParameterKeys(ModelGraph content)
    {
        return content.Parameters.Select(parameter => parameter.ParameterKey).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The keys of the metrics with an attribution.</summary>
    private static HashSet<string> AttributedKeys(ModelGraph content)
    {
        return content.Attributions.Select(attribution => attribution.MetricKey).ToHashSet(StringComparer.Ordinal);
    }
}
