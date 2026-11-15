using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;
using ArticleService.Data;
using ArticleService.Models;

var builder = WebApplication.CreateBuilder(args);

var redisConnection =
    builder.Configuration["Redis:Connection"] ?? "localhost:6379";

builder.Services.AddSingleton<ArticleDatabaseCoordinator>();

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddSingleton<ArticleCache>();
builder.Services.AddHostedService<ArticleCacheWarmer>();
builder.Services.AddControllers();

var app = builder.Build();

// Initialize both regional databases
var coordinator =
    app.Services.GetRequiredService<ArticleDatabaseCoordinator>();

foreach (var region in coordinator.GetRegions())
{
    await using var db = coordinator.GetDatabase(region);

    await db.Database.EnsureCreatedAsync();

    if (!await db.Articles.AnyAsync())
    {
        if (region == "Global")
        {
            db.Articles.AddRange(
                new Article
                {
                    Title = "Hope grows in local gardens",
                    Content = "Community gardens bring people together.",
                    PublishedUtc = DateTime.UtcNow.AddDays(-1),
                    Region = "Global"
                },
                new Article
                {
                    Title = "Young scientists make a discovery",
                    Content = "A new discovery gives researchers hope.",
                    PublishedUtc = DateTime.UtcNow.AddDays(-3),
                    Region = "Global"
                }
            );
        }
        else if (region == "Europe")
        {
            db.Articles.Add(
                new Article
                {
                    Title = "Positive news from Europe",
                    Content = "Communities are helping each other.",
                    PublishedUtc = DateTime.UtcNow.AddDays(-5),
                    Region = "Europe"
                }
            );
        }

        await db.SaveChangesAsync();
    }
}

app.UseRouting();
app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics();

app.Run();

public class ArticleCache(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public static readonly Counter Hits =
        Metrics.CreateCounter(
            "hh_cache_hits_total",
            "Cache hits.",
            new CounterConfiguration { LabelNames = ["cache"] });

    public static readonly Counter Misses =
        Metrics.CreateCounter(
            "hh_cache_misses_total",
            "Cache misses.",
            new CounterConfiguration { LabelNames = ["cache"] });

    private static string Key(Guid id) => $"article:{id}";

    public async Task<Article?> Get(Guid id)
    {
        try
        {
            var value = await _db.StringGetAsync(Key(id));

            if (value.HasValue)
            {
                Hits.WithLabels("article").Inc();

                return System.Text.Json.JsonSerializer
                    .Deserialize<Article>(value!);
            }

            Misses.WithLabels("article").Inc();
            return null;
        }
        catch
        {
            Misses.WithLabels("article").Inc();
            return null;
        }
    }

    public async Task Set(Article article)
    {
        try
        {
            await _db.StringSetAsync(
                Key(article.Id),
                System.Text.Json.JsonSerializer.Serialize(article),
                TimeSpan.FromMinutes(30));
        }
        catch { }
    }

    public async Task Remove(Guid id)
    {
        try
        {
            await _db.KeyDeleteAsync(Key(id));
        }
        catch { }
    }

    public async Task Warm(IEnumerable<Article> articles)
    {
        foreach (var article in articles)
            await Set(article);
    }
}

public class ArticleCacheWarmer(
    ArticleDatabaseCoordinator coordinator,
    ArticleCache cache,
    ILogger<ArticleCacheWarmer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var region in coordinator.GetRegions())
                {
                    await using var db = coordinator.GetDatabase(region);

                    var articles = await db.Articles
                        .Where(a =>
                            a.Region == region &&
                            a.PublishedUtc >= DateTime.UtcNow.AddDays(-14))
                        .ToListAsync(stoppingToken);

                    await cache.Warm(articles);

                    logger.LogInformation(
                        "Article cache warmed for {Region} with {Count} articles",
                        region,
                        articles.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Article cache warmer failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}