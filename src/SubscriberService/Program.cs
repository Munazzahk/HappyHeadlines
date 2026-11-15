
using System.Collections.Concurrent;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Simple in-memory storage
var subscribers = new ConcurrentDictionary<string, DateTime>();

app.UseHttpMetrics();

app.MapPost("/api/subscribers", (SubscribeRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Email))
        return Results.BadRequest("Email is required.");

    var email = request.Email.Trim().ToLowerInvariant();

    var added = subscribers.TryAdd(email, DateTime.UtcNow);

    if (!added)
        return Results.Conflict("Already subscribed.");

    return Results.Ok(new
    {
        Message = "Successfully subscribed.",
        Email = email
    });
});

app.MapGet("/health", () => Results.Ok("Healthy"));

app.MapMetrics();

app.Run();

record SubscribeRequest(string Email);
