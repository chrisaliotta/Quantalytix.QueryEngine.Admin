using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The parameters calculation models declare for their formulas, each written whole by key. Parameters are live like the formulas
/// that read them and the report rows that set them, so <see cref="ModelChanges"/> checks every write on the model it would leave.
/// </summary>
public sealed class ModelParameterService(ITableGateway<ModelParameterRecord> parameters)
{
    /// <summary>Lists a model's parameters.</summary>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The parameters by key.</returns>
    public async Task<IReadOnlyList<ModelParameter>> ListAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        var rows = await parameters.ListAsync(new { CalculationModelId = calculationModelId }, cancellationToken);
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

    /// <summary>The parameter an input describes under a key.</summary>
    /// <param name="parameterKey">The validated key.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The parameter to store.</returns>
    public static ModelParameter Compose(string parameterKey, ModelParameterInput input)
    {
        return new ModelParameter
        {
            ParameterKey = parameterKey,
            DisplayName = input.DisplayName,
            ValueType = input.ValueType,
            AllowedValues = input.AllowedValues,
            DefaultNumber = input.DefaultNumber,
            DefaultText = input.DefaultText,
            Description = input.Description
        };
    }

    /// <summary>The model with a parameter added, or in place of the stored one with its key.</summary>
    /// <param name="model">The model as it stands.</param>
    /// <param name="parameter">The composed parameter.</param>
    /// <returns>The model as the write leaves it.</returns>
    public static ModelGraph Put(ModelGraph model, ModelParameter parameter)
    {
        return model with { Parameters = [.. Without(model, parameter.ParameterKey).Parameters, parameter] };
    }

    /// <summary>The model without a parameter.</summary>
    /// <param name="model">The model as it stands.</param>
    /// <param name="parameterKey">The parameter.</param>
    /// <returns>The model as the delete leaves it.</returns>
    public static ModelGraph Without(ModelGraph model, string parameterKey)
    {
        return model with { Parameters = [.. model.Parameters.Where(parameter => parameter.ParameterKey != parameterKey)] };
    }

    /// <summary>Stores a parameter in place of the one with its key; a primary key insert replaces the stored row.</summary>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="parameter">A parameter the model gate let through.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the parameter is stored.</returns>
    public Task ReplaceAsync(long calculationModelId, ModelParameter parameter, CancellationToken cancellationToken)
    {
        return parameters.InsertAsync(new ModelParameterRecord
        {
            CalculationModelId = calculationModelId,
            ParameterKey = parameter.ParameterKey,
            DisplayName = parameter.DisplayName,
            ValueType = parameter.ValueType,
            AllowedValuesJson = parameter.AllowedValues is { } allowed ? JsonSerializer.Serialize(allowed) : null,
            DefaultNumber = parameter.DefaultNumber,
            DefaultText = parameter.DefaultText,
            Description = parameter.Description
        }, cancellationToken);
    }

    /// <summary>Deletes a parameter.</summary>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="parameterKey">The parameter.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the parameter is gone.</returns>
    public Task DeleteAsync(long calculationModelId, string parameterKey, CancellationToken cancellationToken)
    {
        return parameters.DeleteAsync(new { CalculationModelId = calculationModelId, ParameterKey = parameterKey }, cancellationToken);
    }
}
