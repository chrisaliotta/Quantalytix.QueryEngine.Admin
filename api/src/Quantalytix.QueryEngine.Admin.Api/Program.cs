using FluentValidation;
using Quantalytix.QueryEngine.Admin.Api.Authentication;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Configuration;
using Quantalytix.QueryEngine.Admin.Api.Development;
using Quantalytix.QueryEngine.Admin.Application.Configuration;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAdminAuthentication();
if (builder.Environment.IsDevelopment()) builder.Services.AddDevelopmentTokens();
builder.Services.AddAdminAuthorization();
builder.Services.AddAdminTenancy();
builder.Services.AddAdminControllers(builder.Environment);
builder.Services.AddValidatorsFromAssemblyContaining(typeof(AdminApplicationServiceCollectionExtensions));
builder.Services.AddProblemDetails();
builder.Services.AddAdminOpenApi();
builder.Services.AddAdminCors();
builder.Services.AddHealthChecks();
builder.Services.AddAdminApplication();
builder.Services.AddStarRocksInfrastructure(builder.Configuration);
builder.Services.AddDatasetsFeature();
builder.Services.AddDimensionsFeature();
builder.Services.AddMappingsAndBridgesFeature();
builder.Services.AddBindingsAndMeasuresFeature();
builder.Services.AddReleasesAndSystemFeature();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
