using Quantalytix.QueryEngine.Admin.Application.Content;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Model content edited row by row: the variables and calculated measures of a calculation model. Each kind knows what a write does
/// to the model's metrics, so <see cref="ModelChanges"/> can check a write before it is made.
/// </summary>
/// <typeparam name="TItem">The content row as the API returns it.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
public interface IModelContent<TItem, TInput> : IOwnedContent<TItem, TInput>
{
    /// <summary>The model with a row added, or in place of the stored row with its ID.</summary>
    /// <param name="model">The model's metrics as they stand.</param>
    /// <param name="item">A row <see cref="IOwnedContent{TItem, TInput}.Compose"/> made.</param>
    /// <returns>The model's metrics as the write leaves them.</returns>
    ModelGraph Put(ModelGraph model, TItem item);

    /// <summary>The model without a row.</summary>
    /// <param name="model">The model's metrics as they stand.</param>
    /// <param name="itemId">The row, one of the model's.</param>
    /// <returns>The model's metrics as the delete leaves them.</returns>
    ModelGraph Without(ModelGraph model, long itemId);
}
