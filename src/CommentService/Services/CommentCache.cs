
using CommentService.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace CommentService.Services;

public class CommentCache(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    private static string Key(Guid articleId)
        => $"comments:{articleId}";

    public async Task<List<Comment>?> Get(Guid articleId)
    {
        try
        {
            var value = await _db.StringGetAsync(Key(articleId));

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<List<Comment>>(value!);
        }
        catch
        {
            // Redis should not prevent comments from being retrieved.
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
            // Redis failure should not prevent comment operations.
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
