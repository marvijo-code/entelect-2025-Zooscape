using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .CreateBootstrapLogger(); // Use CreateBootstrapLogger for initial logging before host is built
    
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .MinimumLevel.Debug()
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("c:\\dev\\2025-Zooscape\\api.log", rollingInterval: RollingInterval.Day));

    // Add services
    builder.Services.AddSerilog(); // Ensures Serilog.ILogger is available for direct injection
    builder.Services.AddControllers();
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        });
    });

    // Add Swagger for API documentation
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSingleton<FunctionalTests.Services.CacheService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.MapControllers();

    // Default route for health check
    app.MapGet("/", () => "Functional Tests API is running!");

    Log.Information("Starting API server on http://localhost:5008");
    
    app.Run("http://localhost:5008");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
} 
