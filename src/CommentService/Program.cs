using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var db = builder.Configuration.GetConnectionString("Comments")
         ?? "Host=localhost;Port=5434;Database=comments;Username=hh;Password=hh";
var redisConnection = builder.Configuration["Redis:Connection"] ?? "localhost:6380";

builder.Services.AddDbContext<CommentDbContext>(o => o.UseNpgsql(db));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<CommentCache>();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CommentDbContext>().Database.EnsureCreated();
}

app.MapControllers();
app.MapMetrics();
app.Run();

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class CommentDbContext(DbContextOptions<CommentDbContext> options) : DbContext(options)
{
    public DbSet<Comment> Comments => Set<Comment>();
}

public class CommentCache(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string LruKey = "comments:lru";
    private const int MaxArticles = 30;

    public static readonly Counter Hits = Metrics.CreateCounter("hh_cache_hits_total", "Cache hits.", new CounterConfiguration { LabelNames = ["cache"] });
    public static readonly Counter Misses = Metrics.CreateCounter("hh_cache_misses_total", "Cache misses.", new CounterConfiguration { LabelNames = ["cache"] });

    private static string Key(Guid articleId) => $"comments:{articleId}";

    public async Task<List<Comment>?> Get(Guid articleId)
    {
        try
        {
            var value = await _db.StringGetAsync(Key(articleId));
            if (!value.HasValue)
            {
                Misses.WithLabels("comment").Inc();
                return null;
            }

            Hits.WithLabels("comment").Inc();
            await Touch(articleId);
            return System.Text.Json.JsonSerializer.Deserialize<List<Comment>>(value!);
        }
        catch
        {
            Misses.WithLabels("comment").Inc();
            return null;
        }
    }

    public async Task Set(Guid articleId, List<Comment> comments)
    {
        try
        {
            await _db.StringSetAsync(Key(articleId), System.Text.Json.JsonSerializer.Serialize(comments));
            await Touch(articleId);
            var count = await _db.SortedSetLengthAsync(LruKey);
            if (count > MaxArticles)
            {
                var oldest = await _db.SortedSetRangeByRankAsync(LruKey, 0, count - MaxArticles - 1);
                foreach (var member in oldest)
                {
                    if (Guid.TryParse(member!, out var oldArticle))
                        await _db.KeyDeleteAsync(Key(oldArticle));
                    await _db.SortedSetRemoveAsync(LruKey, member);
                }
            }
        }
        catch { }
    }

    public async Task Remove(Guid articleId)
    {
        try
        {
            await _db.KeyDeleteAsync(Key(articleId));
            await _db.SortedSetRemoveAsync(LruKey, articleId.ToString());
        }
        catch { }
    }

    private Task Touch(Guid articleId) =>
        _db.SortedSetAddAsync(LruKey, articleId.ToString(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
