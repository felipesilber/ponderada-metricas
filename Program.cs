using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;


// Custom metrics for the application
var greeterMeter = new Meter("OtPrGrYa.Example", "1.0.0");
var countGreetings = greeterMeter.CreateCounter<int>("greetings.count", description: "Counts the number of greetings");

// Custom ActivitySource for the application
var greeterActivitySource = new ActivitySource("OtPrGrJa.Example");

var builder = WebApplication.CreateBuilder(args);

var tracingOtlpEndpoint = builder.Configuration["OTLP_ENDPOINT_URL"];
var otel = builder.Services.AddOpenTelemetry();

otel.ConfigureResource(resource => resource
    .AddService(serviceName: builder.Environment.ApplicationName));

otel.WithMetrics(metrics => metrics
    // Metrics provider from OpenTelemetry
    .AddAspNetCoreInstrumentation()
    .AddMeter(greeterMeter.Name)
    // Metrics provides by ASP.NET Core in .NET 8
    .AddMeter("Microsoft.AspNetCore.Hosting")
    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
    .AddPrometheusExporter());

otel.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddHttpClientInstrumentation();
    tracing.AddSource(greeterActivitySource.Name);
    if (tracingOtlpEndpoint != null)
    {
        tracing.AddOtlpExporter(otlpOptions =>
         {
             otlpOptions.Endpoint = new Uri(tracingOtlpEndpoint);
         });
    }
    else
    {
        tracing.AddConsoleExporter();
    }
});

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();

app.MapGet("/", SendGreeting);

async Task<String> SendGreeting(ILogger<Program> logger)
{
    // Create a new Activity scoped to the method
    using var activity = greeterActivitySource.StartActivity("GreeterActivity");

    // Log a message
    logger.LogInformation("Sending greeting");

    // Increment the custom counter
    countGreetings.Add(1);

    // Add a tag to the Activity
    activity?.SetTag("greeting", "Hello World!");

    return "Hello World!";
}



app.Run();
