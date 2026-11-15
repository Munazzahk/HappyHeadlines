
using CommentService.Data;
using CommentService.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
// Distributed tracing with OpenTelemetry and Zipkin
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("comment-service"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddZipkinExporter(options =>
        {
            options.Endpoint = new Uri("http://zipkin:9411/api/v2/spans");
        }));

// Database connection
var connectionString =
    builder.Configuration.GetConnectionString("Comments")
    ?? throw new InvalidOperationException(
        "Comments connection string was not found.");

builder.Services.AddDbContext<CommentDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis cache
var redisConnection =
    builder.Configuration["Redis:Connection"] ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddSingleton<CommentCache>();

// Profanity service
builder.Services.AddSingleton<ProfanityCircuitBreaker>();

builder.Services.AddHttpClient<ProfanityClient>(client =>
{
    client.BaseAddress = new Uri("http://profanity-service:8080/");
    client.Timeout = TimeSpan.FromSeconds(3);
});


// API
builder.Services.AddControllers();

var app = builder.Build();

// Create database if it does not exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<CommentDbContext>();

    await db.Database.EnsureCreatedAsync();
}

app.UseRouting();
app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics();

app.Run();
