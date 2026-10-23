using FluentValidation.Results;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SystemTenants;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.SystemTenants;

/// <summary>
/// Confirms that a tenant connection string is accepted only when the database it reaches belongs to the tenant, has the
/// metadata schema version this API supports and holds exactly one active assumption set: a tenant-key mismatch, a wrong
/// schema version, a missing or repeated active set, or an unreachable database becomes an error on <c>connectionString</c>
/// that gives the reason, and a good connection passes. Every publish repoints the active set, so the API refuses a
/// database without exactly one. On an edit, a supplied connection string is
/// verified against the route's tenant and an omitted one is not verified at all. The tenant directory is the engine's
/// only routing table, so a wrong route would serve one tenant another tenant's metadata.
/// </summary>
public sealed class TenantInputValidatorTests
{
    /// <summary>The tenant being registered or edited.</summary>
    private const string TenantId = "sandbox";

    /// <summary>The candidate connection string; the fake verifier never opens it.</summary>
    private const string ConnectionString = "Server=starrocks.example;Port=9030;Database=srdb_admin_sandbox;User ID=admin;Password=secret";

    /// <summary>A tenant key that differs from the database's.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task TenantKeyMismatch_Fails()
    {
        var result = await ValidateCreateAsync(Reached("development", TenantConnectionVerification.SupportedSchemaVersion));

        Assert.Equal("The database belongs to tenant 'development', not 'sandbox'.", ConnectionStringError(result));
    }

    /// <summary>A database with another metadata schema version.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task WrongSchemaVersion_Fails()
    {
        var result = await ValidateCreateAsync(Reached(TenantId, 3));

        Assert.Equal("The database has metadata schema version 3; this API requires version 16.", ConnectionStringError(result));
    }

    /// <summary>A database without an active assumption set, or with two, fails.</summary>
    /// <param name="activeSets">The number of active assumption sets the database holds.</param>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ActiveAssumptionSetsOtherThanOne_Fail(int activeSets)
    {
        var result = await ValidateCreateAsync(Reached(TenantId, TenantConnectionVerification.SupportedSchemaVersion) with { ActiveAssumptionSets = activeSets });

        Assert.Equal(
            $"The database has {activeSets} active assumption sets; it needs exactly one, which holds the default versions.",
            ConnectionStringError(result));
    }

    /// <summary>A connection that cannot be opened reports the verifier's reason.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task UnreachableDatabase_GivesTheReason()
    {
        var result = await ValidateCreateAsync(new TenantDatabaseProbe { Failure = "Unable to connect to any of the specified MySQL hosts." });

        Assert.Equal("Unable to connect to any of the specified MySQL hosts.", ConnectionStringError(result));
    }

    /// <summary>A connection to the tenant's own database at the supported schema version passes.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task GoodConnection_Passes()
    {
        var result = await ValidateCreateAsync(Reached(TenantId, TenantConnectionVerification.SupportedSchemaVersion));

        Assert.True(result.IsValid);
    }

    /// <summary>An edit verifies a supplied connection string against the tenant the route names.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task Update_VerifiesAgainstTheRouteTenant()
    {
        var verifier = new FakeTenantDatabaseVerifier(Reached("development", TenantConnectionVerification.SupportedSchemaVersion));

        var result = await ValidateUpdateAsync(verifier, ConnectionString);

        Assert.Equal("The database belongs to tenant 'development', not 'sandbox'.", ConnectionStringError(result));
        Assert.Equal([ConnectionString], verifier.ProbedConnectionStrings);
    }

    /// <summary>An edit without a connection string keeps the current one, so nothing is probed.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task Update_WithoutConnectionString_IsNotVerified()
    {
        var verifier = new FakeTenantDatabaseVerifier(new TenantDatabaseProbe { Failure = "Unreachable." });

        var result = await ValidateUpdateAsync(verifier, null);

        Assert.True(result.IsValid);
        Assert.Empty(verifier.ProbedConnectionStrings);
    }

    /// <summary>Validates the registration of <see cref="TenantId"/> against a fake database that answers with the given probe.</summary>
    private static Task<ValidationResult> ValidateCreateAsync(TenantDatabaseProbe probe)
    {
        var validator = new TenantCreateInputValidator(
            new InMemoryTableGateway<TenantDirectoryRecord>(),
            new TenantConnectionVerification(new FakeTenantDatabaseVerifier(probe)));
        var input = new TenantCreateInput
        {
            TenantId = TenantId,
            ProviderId = ProviderIds.StarRocks,
            ConnectionString = ConnectionString,
            IsEnabled = true,
            MetadataCacheSeconds = 60
        };
        return validator.ValidateAsync(input, TestContext.Current.CancellationToken);
    }

    /// <summary>Validates an edit of <see cref="TenantId"/>, the route's tenant, with the given connection string.</summary>
    private static Task<ValidationResult> ValidateUpdateAsync(FakeTenantDatabaseVerifier verifier, string? connectionString)
    {
        var validator = new TenantUpdateInputValidator(new FixedTenantContext(TenantId), new TenantConnectionVerification(verifier));
        var input = new TenantUpdateInput { ProviderId = ProviderIds.StarRocks, ConnectionString = connectionString, MetadataCacheSeconds = 60 };
        return validator.ValidateAsync(input, TestContext.Current.CancellationToken);
    }

    /// <summary>A probe that reached a database with the given tenant key and schema version, and one active assumption set.</summary>
    private static TenantDatabaseProbe Reached(string tenantKey, int metadataSchemaVersion)
    {
        return new TenantDatabaseProbe
        {
            ActiveAssumptionSets = 1,
            DatabaseInfo = new DatabaseInfoRecord
            {
                SingletonId = 1,
                TenantKey = tenantKey,
                MetadataSchemaVersion = metadataSchemaVersion,
                SemanticRevision = 12,
                UpdatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };
    }

    /// <summary>The single error, which must be on <c>connectionString</c>; the API names it in camelCase, plain validation in PascalCase.</summary>
    private static string ConnectionStringError(ValidationResult result)
    {
        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(TenantCreateInput.ConnectionString), error.PropertyName, StringComparer.OrdinalIgnoreCase);
        return error.ErrorMessage;
    }
}
