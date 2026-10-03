using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/articles")]
public class ArticlesController(ArticleDbContext db, ArticleCache cache) : ControllerBase
{
    [HttpGet("global")]
    public async Task<IActionResult> Global()
    {
        var articles = await db.Articles
            .Where(a => a.Region == "Global" && a.PublishedUtc >= DateTime.UtcNow.AddDays(-14))
            .OrderByDescending(a => a.PublishedUtc)
            .ToListAsync();

        var cached = new List<Article>();
        foreach (var a in articles)
        {
            var value = await cache.Get(a.Id);
            if (value is not null) cached.Add(value);
        }

        // The endpoint demonstrates the cache by returning cached values where available.
        // The database query remains the source of truth.
        return Ok(cached.Count == articles.Count ? cached : articles);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var cached = await cache.Get(id);
        if (cached is not null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cached);
        }

        var article = await db.Articles.FindAsync(id);
        if (article is null) return NotFound();

        await cache.Set(article);
        Response.Headers["X-Cache"] = "MISS";
        return Ok(article);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Article article)
    {
        article.Id = Guid.NewGuid();
        if (article.PublishedUtc == default) article.PublishedUtc = DateTime.UtcNow;
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        if (article.Region == "Global" && article.PublishedUtc >= DateTime.UtcNow.AddDays(-14))
            await cache.Set(article);

        return CreatedAtAction(nameof(Get), new { id = article.Id }, article);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, Article input)
    {
        var article = await db.Articles.FindAsync(id);
        if (article is null) return NotFound();

        article.Title = input.Title;
        article.Content = input.Content;
        article.PublishedUtc = input.PublishedUtc;
        article.Region = input.Region;
        await db.SaveChangesAsync();
        await cache.Remove(id);

        return Ok(article);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var article = await db.Articles.FindAsync(id);
        if (article is null) return NotFound();

        db.Articles.Remove(article);
        await db.SaveChangesAsync();
        await cache.Remove(id);
        return NoContent();
    }

    [HttpGet("instance")]
    public IActionResult Instance() => Ok(new { instance = Environment.GetEnvironmentVariable("INSTANCE_ID") ?? "local" });
}
