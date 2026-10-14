#:package MySqlConnector@2.6.1

// Rebuilds the sandbox tenant database as a copy of the development tenant's database and registers tenant 'sandbox' in
// the control plane, so admin development never writes to the database behind the query engine's golden cases.
// Usage: QE_STARROCKS_CONNECTION="Server=<host>;Port=9030;Database=query_control;User ID=<user>;Password=<password>;" dotnet run CreateSandboxTenant.cs

using MySqlConnector;

const string SourceTenantId = "development";
const string SandboxTenantId = "sandbox";
const string SandboxDatabase = "srdb_admin_sandbox";
const string PropertiesStart = "PROPERTIES (";

var controlConnectionString = Environment.GetEnvironmentVariable("QE_STARROCKS_CONNECTION")
    ?? throw new InvalidOperationException("Set QE_STARROCKS_CONNECTION to the query_control connection string.");

await using var control = await OpenAsync(controlConnectionString);
var sourceRoute = (string)(await ScalarAsync(control, "SELECT connection_string FROM qe_tenant_directory WHERE tenant_id = @TenantId",
    ("@TenantId", SourceTenantId)))!;
var sourceDatabase = new MySqlConnectionStringBuilder(sourceRoute).Database;
var sandboxRoute = new MySqlConnectionStringBuilder(sourceRoute) { Database = SandboxDatabase }.ConnectionString;

await using var server = await OpenAsync(controlConnectionString);
await ExecuteAsync(server, $"DROP DATABASE IF EXISTS `{SandboxDatabase}` FORCE");
await ExecuteAsync(server, $"CREATE DATABASE `{SandboxDatabase}`");
await ExecuteAsync(server, $"USE `{SandboxDatabase}`");

var objects = await ListObjectsAsync(server, sourceDatabase);
var foreignKeys = new List<(string Table, string Constraints)>();
foreach (var table in objects.Where(item => item.Type == "BASE TABLE"))
{
    var ddl = await ShowCreateAsync(server, $"SHOW CREATE TABLE `{sourceDatabase}`.`{table.Name}`");
    var (statement, constraints) = PrepareTable(ddl, sourceDatabase, table.Name);
    await ExecuteAsync(server, statement);
    if (constraints is not null) foreignKeys.Add((table.Name, constraints));
    await ExecuteAsync(server, $"INSERT INTO `{SandboxDatabase}`.`{table.Name}` SELECT * FROM `{sourceDatabase}`.`{table.Name}`");
    Console.WriteLine($"table {table.Name}");
}

// View bodies are unqualified, so they resolve against the sandbox database selected above.
foreach (var view in objects.Where(item => item.Type == "VIEW"))
{
    await ExecuteAsync(server, await ShowCreateAsync(server, $"SHOW CREATE VIEW `{sourceDatabase}`.`{view.Name}`"));
    Console.WriteLine($"view {view.Name}");
}

foreach (var (table, constraints) in foreignKeys)
    await ExecuteAsync(server, $"ALTER TABLE `{SandboxDatabase}`.`{table}` SET (\"foreign_key_constraints\" = \"{constraints}\")");

await ExecuteAsync(server, $"UPDATE `{SandboxDatabase}`.qe_database_info SET tenant_key = @TenantKey WHERE singleton_id = 1",
    ("@TenantKey", SandboxTenantId));
await ExecuteAsync(control,
    """
    INSERT INTO qe_tenant_directory (tenant_id, provider_id, connection_string, is_enabled, metadata_cache_seconds, updated_at)
    VALUES (@TenantId, 'starrocks', @ConnectionString, 1, 60, @UpdatedAt)
    """,
    ("@TenantId", SandboxTenantId), ("@ConnectionString", sandboxRoute), ("@UpdatedAt", DateTime.UtcNow));

Console.WriteLine($"Tenant '{SandboxTenantId}' now routes to {SandboxDatabase}, copied from {sourceDatabase}.");

static async Task<MySqlConnection> OpenAsync(string connectionString)
{
    var builder = new MySqlConnectionStringBuilder(connectionString) { ConnectionReset = false, DefaultCommandTimeout = 600 };
    var connection = new MySqlConnection(builder.ConnectionString);
    await connection.OpenAsync();
    return connection;
}

static async Task ExecuteAsync(MySqlConnection connection, string sql, params (string Name, object Value)[] parameters)
{
    await using var command = Command(connection, sql, parameters);
    await command.ExecuteNonQueryAsync();
}

static async Task<object?> ScalarAsync(MySqlConnection connection, string sql, params (string Name, object Value)[] parameters)
{
    await using var command = Command(connection, sql, parameters);
    return await command.ExecuteScalarAsync();
}

static MySqlCommand Command(MySqlConnection connection, string sql, (string Name, object Value)[] parameters)
{
    var command = new MySqlCommand(sql, connection);
    foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
    return command;
}

static async Task<string> ShowCreateAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    return reader.GetString(1);
}

static async Task<List<(string Name, string Type)>> ListObjectsAsync(MySqlConnection connection, string database)
{
    await using var command = Command(connection,
        "SELECT table_name, table_type FROM information_schema.tables WHERE table_schema = @Schema ORDER BY table_name",
        [("@Schema", database)]);
    await using var reader = await command.ExecuteReaderAsync();
    var objects = new List<(string Name, string Type)>();
    while (await reader.ReadAsync()) objects.Add((reader.GetString(0), reader.GetString(1)));
    return objects;
}

// SHOW CREATE TABLE qualifies constraint properties with the source database. Foreign keys are removed from the CREATE
// statement and returned for re-application once every table exists; unique constraints lose their table qualifier.
static (string Statement, string? ForeignKeys) PrepareTable(string ddl, string sourceDatabase, string table)
{
    var start = ddl.IndexOf(PropertiesStart, StringComparison.Ordinal);
    var body = ddl[(start + PropertiesStart.Length)..ddl.LastIndexOf(')')];
    string? foreignKeys = null;
    var properties = new List<string>();
    foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var property = line.TrimEnd(',');
        if (property.StartsWith("\"foreign_key_constraints\"", StringComparison.Ordinal))
        {
            var value = property[(property.IndexOf("= \"", StringComparison.Ordinal) + 3)..^1];
            foreignKeys = value.Replace($"default_catalog.{sourceDatabase}.", $"default_catalog.{SandboxDatabase}.", StringComparison.Ordinal);
            continue;
        }

        properties.Add(property.Replace($"default_catalog.{sourceDatabase}.{table}.", "", StringComparison.Ordinal));
    }

    return ($"{ddl[..start]}{PropertiesStart}\n{string.Join(",\n", properties)}\n)", foreignKeys);
}
