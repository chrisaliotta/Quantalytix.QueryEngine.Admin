var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { service = "Quantalytix.QueryEngine.Admin.Api", status = "ok" }));

app.Run();
