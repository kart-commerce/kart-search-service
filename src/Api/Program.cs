using FluentValidation.AspNetCore;
using Kart.Search.Api.HealthChecks;
using Kart.Search.Api.Middleware;
using Kart.Search.Application;
using Kart.Search.Application.Common.Exceptions;
using Kart.Search.Infrastructure;
using Kart.Shared.Configuration;
using Kart.Shared.ErrorHandling;
using Kart.Shared.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service - never reimplemented per service. See appsettings.Local.json.example.
builder.AddKartGlobalConfig("kart-search-service");

builder.AddKartObservability("kart-search-service");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Search is a pure read-only, query-side service (requirement-spec.md §1) - GET /v1/search grants
// an unconditional CanRead to any principal, including unauthenticated browse traffic. There is
// no externally-reachable write endpoint of any kind for CanWrite/CanDelete to gate
// (ddd-model.md's CanRead/CanWrite/CanDelete invariant) - no authentication middleware is wired.
builder.Services.AddKartErrorHandling(options => options
    .Map<FacetFilterLimitExceededException>(StatusCodes.Status400BadRequest, "FACET_FILTER_LIMIT_EXCEEDED")
    .Map<PaginationWindowExceededException>(StatusCodes.Status400BadRequest, "PAGINATION_WINDOW_EXCEEDED"));

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// /health/live: process is up, no dependency check. /health/ready: this service's entire job
// depends on OpenSearch (its one and only data store), so readiness genuinely checks it responds -
// matching kart-infra's service-chart probe convention.
builder.Services.AddHealthChecks()
    .AddCheck<OpenSearchHealthCheck>("opensearch", tags: ["ready"]);

var app = builder.Build();

// The global exception handler is the only place any exception reaching the HTTP boundary is
// caught and translated - registered first so it wraps everything downstream.
app.UseKartErrorHandling();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseMiddleware<SearchContextEnrichmentMiddleware>();

app.MapControllers();
app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

/// <summary>Exposes the implicitly-generated Program class to WebApplicationFactory&lt;Program&gt; in ContractTests/IntegrationTests.</summary>
public partial class Program;
