using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Reads the parameters calculation models declare for their formulas, which the report rules and the report editor read too, and
/// maps them to the rows a model change set writes.
/// </summary>
/// <param name="parameters">The parameter table.</param>
public sealed class ModelParameterService(ITableGateway<ModelParameterRecord> parameters)
{
    /// <summary>Lists a model version's parameters.</summary>
    /// <param name="modelVersionId">The calculation model version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The parameters by key.</returns>
    public async Task<IReadOnlyList<ModelParameter>> ListAsync(long modelVersionId, CancellationToken cancellationToken)
    {
        var rows = await parameters.ListAsync(new { CalculationModelVersionId = modelVersionId }, cancellationToken);
        return
        [
            .. rows.OrderBy(row => row.ParameterKey, StringComparer.Ordinal).Select(row => new ModelParameter
            {
                ParameterKey = row.ParameterKey,
                DisplayName = row.DisplayName,
                ValueType = row.ValueType,
                AllowedValues = row.AllowedValuesJson is { } json ? JsonSerializer.Deserialize<string[]>(json) : null,
                DefaultNumber = row.DefaultNumber,
                DefaultText = row.DefaultText,
                Description = row.Description
            })
        ];
    }

    /// <summary>The row a model's parameter is stored as.</summary>
    /// <param name="modelVersionId">The calculation model version.</param>
    /// <param name="parameter">The parameter.</param>
    /// <returns>The row.</returns>
    public static ModelParameterRecord ToRecord(long modelVersionId, ModelParameter parameter)
    {
        return new ModelParameterRecord
        {
            CalculationModelVersionId = modelVersionId,
            ParameterKey = parameter.ParameterKey,
            DisplayName = parameter.DisplayName,
            ValueType = parameter.ValueType,
            AllowedValuesJson = parameter.AllowedValues is { } allowed ? JsonSerializer.Serialize(allowed) : null,
            DefaultNumber = parameter.DefaultNumber,
            DefaultText = parameter.DefaultText,
            Description = parameter.Description
        };
    }
}
