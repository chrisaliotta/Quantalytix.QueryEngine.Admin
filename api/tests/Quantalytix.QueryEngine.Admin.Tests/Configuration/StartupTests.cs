using System.Linq.Expressions;
using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Configuration;

/// <summary>
/// Runs <see cref="StartupTests"/> alone, after every parallel test: starting the API sets FluentValidation's process-wide name
/// resolvers, which would change the messages the validator tests running beside it expect.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StartupCollection
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Startup";
}

/// <summary>
/// Confirms that the Admin API starts: in Development the host checks, before it answers, that every registered service can be
/// constructed. Unit tests build their services by hand, so a registration nothing can construct, such as a validator the
/// assembly scan registers that only another validator can build, passes them all and stops the API at startup.
/// </summary>
[Collection(StartupCollection.Name)]
public sealed class StartupTests : IDisposable
{
    /// <summary>FluentValidation's property name resolver before the API replaced it.</summary>
    private readonly Func<Type, MemberInfo, LambdaExpression, string> _propertyNames = ValidatorOptions.Global.PropertyNameResolver;

    /// <summary>FluentValidation's display name resolver before the API replaced it.</summary>
    private readonly Func<Type, MemberInfo, LambdaExpression, string> _displayNames = ValidatorOptions.Global.DisplayNameResolver;

    /// <summary>The API starts in Development with every registered service constructible.</summary>
    [Fact]
    public void Api_StartsInDevelopment()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host.UseEnvironment(Environments.Development));

        Assert.NotNull(factory.Services);
    }

    /// <summary>Restores FluentValidation's name resolvers for the tests that run after this one.</summary>
    public void Dispose()
    {
        ValidatorOptions.Global.PropertyNameResolver = _propertyNames;
        ValidatorOptions.Global.DisplayNameResolver = _displayNames;
    }
}
