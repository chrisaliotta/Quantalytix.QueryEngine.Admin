namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>Hides the credentials of a tenant connection string before it leaves the server; parsing one is a driver concern.</summary>
public interface IConnectionStringMasker
{
    /// <summary>Replaces the connection string's password with <c>*****</c> and keeps every other part.</summary>
    /// <param name="connectionString">A stored tenant connection string.</param>
    /// <returns>The masked connection string.</returns>
    string Mask(string connectionString);
}
