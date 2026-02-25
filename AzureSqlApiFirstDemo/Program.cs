using Asp.Versioning;
using AzureSqlApiFirstDemo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add API versioning - mimics Azure SQL's version routing mechanism
builder.Services.AddApiVersioning(options =>
{
    // Default to latest stable version (using major.minor format)
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = false;
    
    // Report supported versions in response headers
    options.ReportApiVersions = true;
    
    // Read version from query string (like Azure ARM: ?api-version=2025-08-01)
    options.ApiVersionReader = new QueryStringApiVersionReader("api-version");
    
    // Use custom selector for Azure SQL-style fallback behavior
    options.ApiVersionSelector = new CurrentImplementationApiVersionSelector(options);
})
.AddMvc(options =>
{
    // Add the version fallback convention — this scans controllers at startup
    // and propagates version attributes from newer to older controllers for
    // actions that are missing in the newer version (Azure SQL-style fallback).
    options.Conventions.Add(new VersionFallbackConvention());
})
.AddApiExplorer(options =>
{
    // Format version as 'v2025-08-01' in OpenAPI docs
    options.GroupNameFormat = "'v'yyyy-MM-dd";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<AzureSqlApiFirstDemo.Infrastructure.DatabaseStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
