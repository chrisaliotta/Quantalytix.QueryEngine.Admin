using System.Text.Json;
using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a report version's subtotal position and the shape of its executable JSON: a default filter over calculation dimensions
/// and request parameters only, settings whose <c>periodicity</c>, <c>timeZone</c> and <c>layout</c> are well formed, and a pivot of
/// row and column dimension lists.
/// Whether the dimensions exist, and allow what the defaults ask of them, is the <c>report_defaults_resolve</c> rule's concern,
/// because the version's calculation dimensions can still change. Internal, so the validators that include it are the only ones
/// registered.
/// </summary>
internal sealed class ReportVersionShape : AbstractValidator<ReportVersionInput>
{
    /// <summary>Declares the rules.</summary>
    public ReportVersionShape()
    {
        RuleFor(input => input.SubtotalPosition).MustBeIn(SubtotalPositions.Definition);
        RuleFor(input => input.PayloadJson).MustBeJson();
        RuleFor(input => input.DefaultFilterJson).MustBeFilterOver("dimension", "calculation dimensions and request parameters", readsParameters: true);
        RuleFor(input => input.DefaultSettingsJson).Custom((json, context) =>
        {
            if (json is null || Parse(json, context) is not { } settings) return;
            if (settings.ValueKind != JsonValueKind.Object)
            {
                context.AddFailure("'{PropertyName}' must be a JSON object.");
                return;
            }

            CheckSetting(settings, "periodicity", Periodicities.Definition, context);
            if (settings.TryGetProperty("timeZone", out var timeZone) && (timeZone.ValueKind != JsonValueKind.String || timeZone.GetString()!.Length == 0))
                context.AddFailure("'timeZone' must be an IANA time zone name, such as 'UTC'.");
            CheckSetting(settings, "layout", ReportLayouts.Definition, context);
        });
        RuleFor(input => input.DefaultPivotJson).Custom((json, context) =>
        {
            if (json is null || Parse(json, context) is not { } pivot) return;
            if (pivot.ValueKind != JsonValueKind.Object)
            {
                context.AddFailure("'{PropertyName}' must be a JSON object.");
                return;
            }

            foreach (var property in pivot.EnumerateObject())
            {
                if (property.Name is not ("rowDimensions" or "columnDimensions"))
                    context.AddFailure($"'{property.Name}' is not a pivot entry; a pivot has rowDimensions and columnDimensions.");
                else if (property.Value.ValueKind != JsonValueKind.Array || property.Value.EnumerateArray().Any(key => key.ValueKind != JsonValueKind.String))
                    context.AddFailure($"'{property.Name}' must be an array of calculation-dimension keys.");
            }
        });
    }

    /// <summary>Requires a setting, when present, to be one of a vocabulary's values.</summary>
    private static void CheckSetting(JsonElement settings, string name, Vocabulary.Vocabulary vocabulary, ValidationContext<ReportVersionInput> context)
    {
        if (settings.TryGetProperty(name, out var setting)
            && !vocabulary.Values.Any(value => setting.ValueKind == JsonValueKind.String && value.Value == setting.GetString()))
            context.AddFailure($"'{name}' must be one of {string.Join(", ", vocabulary.Values.Select(value => value.Value))}.");
    }

    /// <summary>Parses JSON text, reporting a parse failure.</summary>
    private static JsonElement? Parse(string json, ValidationContext<ReportVersionInput> context)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException exception)
        {
            context.AddFailure(exception.Message);
            return null;
        }
    }
}
