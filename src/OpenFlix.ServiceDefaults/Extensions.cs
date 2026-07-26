using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    public static TBuilder ValidateProductionSecret<TBuilder>(this TBuilder builder, string key, int minimumLength = 32)
        where TBuilder : IHostApplicationBuilder
    {
        if (!builder.Environment.IsProduction()) return builder;
        var value = builder.Configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.Length < minimumLength ||
            value.Contains("replace", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("development", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("change-me", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Production configuration '{key}' must contain a non-placeholder secret of at least {minimumLength} characters.");
        return builder;
    }

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        var telemetry = builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.WithMetrics(metrics => metrics.AddOtlpExporter())
                .WithTracing(tracing => tracing.AddOtlpExporter());
        }

        builder.Services.AddHttpClient();
        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });
        return app;
    }
}
