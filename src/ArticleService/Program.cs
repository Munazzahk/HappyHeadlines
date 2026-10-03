using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var db = builder.Configuration.GetConnectionString("Articles")
         ?? "Host=localhost;Port=5433;Database=articles;Username=hh;Password=hh";
var redisConnection = builder.Configuration["Redis:Connection"] ?? "localhost:6379";

builder.Services.AddDbContext<ArticleDbContext>(o => o.UseNpgsql(db));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<ArticleCache>();
builder.Services.AddHostedService<ArticleCacheWarmer>();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<ArticleDbContext>();
    ctx.Database.EnsureCreated();
    if (!ctx.Articles.Any())
    {
        ctx.Articles.AddRange(
            new Article { Title = "Hope grows in local gardens", Content = "Community gardens bring people together.", PublishedUtc = DateTime.UtcNow.AddDays(-1), Region = "Global" },
            new Article { Title = "Young scientists make a discovery", Content = "A new discovery gives researchers hope.", PublishedUtc = DateTime.UtcNow.AddDays(-3), Region = "Global" },
            new Article { Title = "Positive news from Europe", Content = "Communities are helping each other.", PublishedUtc = DateTime.UtcNow.AddDays(-5), Region = "Europe" }
        );
        ctx.SaveChanges();
    }
}

app.UseRouting();
app.MapControllers();
app.MapMetrics();

app.Run();

public class Article
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime PublishedUtc { get; set; }
    public string Region { get; set; } = "Global";
}

public class ArticleDbContext(DbContextOptions<ArticleDbContext> options) : DbContext(options)
{
    public DbSet<Article> Articles => Set<Article>();
}

public class ArticleCache(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();
    public static readonly Counter Hits = Metrics.CreateCounter("hh_cache_hits_total", "Cache hits.", new CounterConfiguration { LabelNames = ["cache"] });
    public static readonly Counter Misses = Metrics.CreateCounter("hh_cache_misses_total", "Cache misses.", new CounterConfiguration { LabelNames = ["cache"] });

    private static string Key(Guid id) => $"article:{id}";

    public async Task<Article?> Get(Guid id)
    {
        try
        {
            var value = await _db.StringGetAsync(Key(id));
            if (value.HasValue)
            {
                Hits.WithLabels("article").Inc();
                return System.Text.Json.JsonSerializer.Deserialize<Article>(value!);
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
            await _db.StringSetAsync(Key(article.Id), System.Text.Json.JsonSerializer.Serialize(article), TimeSpan.FromMinutes(30));
        }
        catch { }
    }

    public async Task Remove(Guid id)
    {
        try { await _db.KeyDeleteAsync(Key(id)); } catch { }
    }

    public async Task Warm(IEnumerable<Article> articles)
    {
        foreach (var article in articles) await Set(article);
    }
}

public class ArticleCacheWarmer(IServiceProvider services, ArticleCache cache, ILogger<ArticleCacheWarmer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ArticleDbContext>();
                var articles = await db.Articles
                    .Where(a => a.Region == "Global" && a.PublishedUtc >= DateTime.UtcNow.AddDays(-14))
                    .ToListAsync(stoppingToken);

                await cache.Warm(articles);
                logger.LogInformation("Article cache warmed with {Count} articles", articles.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Article cache warmer failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
