using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("SubscriberService", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:SubscriberServiceUrl"]
        ?? "http://subscriber-service:8080/");

    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();

app.UseHttpMetrics();

app.MapPost("/api/newsletter/subscribe",
    async (SubscribeRequest request,
           IConfiguration configuration,
           IHttpClientFactory httpClientFactory) =>
    {
        // Feature toggle
        var enabled = configuration.GetValue<bool>(
            "Features:SubscriberServiceEnabled");

        // Do not call SubscriberService when disabled
        if (!enabled)
        {
            return Results.Json(
                new { Message = "Newsletter subscription is currently disabled." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            var client = httpClientFactory.CreateClient("SubscriberService");

            var response = await client.PostAsJsonAsync(
                "api/subscribers", request);

            var content = await response.Content.ReadAsStringAsync();

            return Results.Content(
                content,
                "application/json",
                statusCode: (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                "SubscriberService is unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException)
        {
            return Results.Problem(
                "SubscriberService request timed out.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    });

app.MapGet("/health", () => Results.Ok("Healthy"));

app.MapMetrics();
app.Run();

record SubscribeRequest(string Email);
