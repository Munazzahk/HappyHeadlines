
using CommentService.Models;
using Prometheus;
using StackExchange.Redis;
using System.Text.Json;

namespace CommentService.Services;

public class CommentCache(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public static readonly Counter Hits =
        Metrics.CreateCounter(
            "hh_cache_hits_total",
            "Comment cache hits.",
            new CounterConfiguration { LabelNames = ["cache"] });

    public static readonly Counter Misses =
        Metrics.CreateCounter(
            "hh_cache_misses_total",
            "Comment cache misses.",
            new CounterConfiguration { LabelNames = ["cache"] });

    // Initialize the labeled counters so Prometheus can see them,
    // even before the first cache request.
    private static readonly Counter.Child CommentHits =
        Hits.WithLabels("comment");

    private static readonly Counter.Child CommentMisses =
        Misses.WithLabels("comment");

    private static string Key(Guid articleId)
        => $"comments:{articleId}";

    public async Task<List<Comment>?> Get(Guid articleId)
    {
        try
        {
            var value = await _db.StringGetAsync(Key(articleId));

            if (!value.HasValue)
            {
                CommentMisses.Inc();
                return null;
            }

            CommentHits.Inc();

            return JsonSerializer.Deserialize<List<Comment>>(value!);
        }
        catch
        {
            CommentMisses.Inc();
            return null;
        }
    }

    public async Task Set(Guid articleId, List<Comment> comments)
    {
        try
        {
            await _db.StringSetAsync(
                Key(articleId),
                JsonSerializer.Serialize(comments),
                TimeSpan.FromMinutes(30));
        }
        catch
        {
            // Redis should not prevent comments from being retrieved.
        }
    }

    public async Task Remove(Guid articleId)
    {
        try
        {
            await _db.KeyDeleteAsync(Key(articleId));
        }
        catch
        {
            // Ignore cache removal failures.
        }
    }
}
