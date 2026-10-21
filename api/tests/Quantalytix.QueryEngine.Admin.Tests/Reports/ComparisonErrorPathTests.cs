using System.Linq.Expressions;
using System.Reflection;
using FluentValidation;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Configuration;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the key the API gives a default comparison's problem under its own camelCase name resolver: the JSON path of the value
/// inside the settings, such as <c>defaultSettingsJson.comparisons[1].scenario</c>, which the web settings sheet maps onto the
/// comparison row's field. It runs alone in the startup collection, because it sets FluentValidation's process-wide resolvers.
/// </summary>
[Collection(StartupCollection.Name)]
public sealed class ComparisonErrorPathTests : IDisposable
{
    /// <summary>FluentValidation's property name resolver before the test replaced it.</summary>
    private readonly Func<Type, MemberInfo, LambdaExpression, string> _propertyNames = ValidatorOptions.Global.PropertyNameResolver;

    /// <summary>FluentValidation's display name resolver before the test replaced it.</summary>
    private readonly Func<Type, MemberInfo, LambdaExpression, string> _displayNames = ValidatorOptions.Global.DisplayNameResolver;

    /// <summary>A malformed scenario of the second comparison is keyed by its path inside the settings, in camelCase.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task ComparisonProblem_IsKeyedByItsPathInsideTheSettings()
    {
        ValidatorOptions.Global.PropertyNameResolver = JsonPropertyPaths.Resolve;
        ValidatorOptions.Global.DisplayNameResolver = JsonPropertyPaths.Resolve;
        var validator = new ReportSettingsInputValidator(
            new InMemoryTableGateway<SourceProfileRecord>(), new ContentKeys(new FixedRequestRoute(new Dictionary<string, long> { ["reportId"] = ReportId })));
        var input = new ReportSettingsInput
        {
            SubtotalPosition = SubtotalPositions.Above,
            DefaultSettingsJson = """{"comparisons":[{"key":"py","transforms":["change"]},{"key":"fc","scenario":{"latest":""},"transforms":["change"]}]}"""
        };

        var result = await validator.ValidateAsync(input, TestContext.Current.CancellationToken);

        Assert.Equal(["defaultSettingsJson.comparisons[1].scenario"], result.Errors.Select(error => error.PropertyName));
    }

    /// <summary>Restores FluentValidation's name resolvers for the tests that run after this one.</summary>
    public void Dispose()
    {
        ValidatorOptions.Global.PropertyNameResolver = _propertyNames;
        ValidatorOptions.Global.DisplayNameResolver = _displayNames;
    }
}
