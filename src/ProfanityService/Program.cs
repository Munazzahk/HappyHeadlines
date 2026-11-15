
using Prometheus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
// Distributed tracing with OpenTelemetry and Zipkin
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("profanity-service"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddZipkinExporter(options =>
        {
            options.Endpoint = new Uri("http://zipkin:9411/api/v2/spans");
        }));

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpMetrics();
app.MapMetrics();
app.MapControllers();

app.Run();
