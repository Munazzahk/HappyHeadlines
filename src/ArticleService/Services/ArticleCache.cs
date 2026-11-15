using ArticleService.Models;
using Prometheus;
using StackExchange.Redis;
using System.Text.Json;

namespace ArticleService.Services;

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

    private static readonly Counter.Child ArticleHits =
    Hits.WithLabels("article");

    private static readonly Counter.Child ArticleMisses =
        Misses.WithLabels("article");

    private static string Key(Guid id) => $"article:{id}";

    public async Task<Article?> Get(Guid id)
    {
        try
        {
            var value = await _db.StringGetAsync(Key(id));

            if (value.HasValue)
            {
                ArticleHits.Inc();

                return JsonSerializer.Deserialize<Article>(value!);
            }

            ArticleMisses.Inc();
            return null;
        }
        catch
        {
            ArticleMisses.Inc();
            return null;
        }
    }

    public async Task Set(Article article)
    {
        try
        {
            await _db.StringSetAsync(
                Key(article.Id),
                JsonSerializer.Serialize(article),
                TimeSpan.FromMinutes(30));
        }
        catch
        {
            // Redis failure should not prevent article operations.
        }
    }

    public async Task Remove(Guid id)
    {
        try
        {
            await _db.KeyDeleteAsync(Key(id));
        }
        catch
        {
            // Ignore cache removal failures.
        }
    }

    public async Task Warm(IEnumerable<Article> articles)
    {
        foreach (var article in articles)
        {
            await Set(article);
        }
    }
}