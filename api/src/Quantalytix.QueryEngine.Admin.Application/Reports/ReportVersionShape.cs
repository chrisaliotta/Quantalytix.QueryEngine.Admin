using System.Text.Json;
using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a report version's subtotal position and the shape of its executable JSON: a default filter over calculation dimensions
/// only, settings whose <c>periodicity</c> and <c>timeZone</c> are well formed, and a pivot of row and column dimension lists.
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
        RuleFor(input => input.DefaultFilterJson).MustBeFilterOver("dimension", "calculation dimensions");
        RuleFor(input => input.DefaultSettingsJson).Custom((json, context) =>
        {
            if (json is null || Parse(json, context) is not { } settings) return;
            if (settings.ValueKind != JsonValueKind.Object)
            {
                context.AddFailure("'{PropertyName}' must be a JSON object.");
                return;
            }

            if (settings.TryGetProperty("periodicity", out var periodicity) && !Periodicities.Definition.Values.Any(value =>
                    periodicity.ValueKind == JsonValueKind.String && value.Value == periodicity.GetString()))
                context.AddFailure($"'periodicity' must be one of {string.Join(", ", Periodicities.Definition.Values.Select(value => value.Value))}.");
            if (settings.TryGetProperty("timeZone", out var timeZone) && (timeZone.ValueKind != JsonValueKind.String || timeZone.GetString()!.Length == 0))
                context.AddFailure("'timeZone' must be an IANA time zone name, such as 'UTC'.");
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
