using ArticleService.Data;
using ArticleService.Models;
using ArticleService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Controllers;

[ApiController]
[Route("api/articles")]
public class ArticlesController(
    ArticleDatabaseCoordinator coordinator,
    ArticleCache cache) : ControllerBase
{
    [HttpGet("global")]
    public async Task<IActionResult> Global()
    {
        return await GetByRegion("Global");
    }

    [HttpGet("europe")]
    public async Task<IActionResult> Europe()
    {
        return await GetByRegion("Europe");
    }

    private async Task<IActionResult> GetByRegion(string region)
    {
        await using var db = coordinator.GetDatabase(region);

        var articles = await db.Articles
            .Where(a =>
                a.Region == region &&
                a.PublishedUtc >= DateTime.UtcNow.AddDays(-14))
            .OrderByDescending(a => a.PublishedUtc)
            .ToListAsync();

        var result = new List<Article>();

        foreach (var article in articles)
        {
            var cached = await cache.Get(article.Id);
            result.Add(cached ?? article);
        }

        return Ok(result);
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

        foreach (var region in coordinator.GetRegions())
        {
            await using var db = coordinator.GetDatabase(region);

            var article = await db.Articles
                .FirstOrDefaultAsync(a =>
                    a.Id == id && a.Region == region);

            if (article is not null)
            {
                await cache.Set(article);
                Response.Headers["X-Cache"] = "MISS";
                return Ok(article);
            }
        }

        return NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Article article)
    {
        if (!coordinator.GetRegions()
            .Contains(article.Region, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Region must be Global or Europe.");
        }

        article.Region = article.Region.ToLowerInvariant() == "europe"
            ? "Europe"
            : "Global";

        article.Id = Guid.NewGuid();

        if (article.PublishedUtc == default)
            article.PublishedUtc = DateTime.UtcNow;

        await using var db = coordinator.GetDatabase(article.Region);

        db.Articles.Add(article);
        await db.SaveChangesAsync();

        await cache.Set(article);

        return CreatedAtAction(
            nameof(Get),
            new { id = article.Id },
            article);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, Article input)
    {
        if (!coordinator.GetRegions()
            .Contains(input.Region, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Region must be Global or Europe.");
        }

        input.Region = input.Region.ToLowerInvariant() == "europe"
            ? "Europe"
            : "Global";

        foreach (var region in coordinator.GetRegions())
        {
            await using var db = coordinator.GetDatabase(region);

            var article = await db.Articles.FindAsync(id);

            if (article is null)
                continue;

            article.Title = input.Title;
            article.Content = input.Content;
            article.PublishedUtc = input.PublishedUtc;
            article.Region = input.Region;

            if (region != input.Region)
            {
                db.Articles.Remove(article);
                await db.SaveChangesAsync();

                await using var destination =
                    coordinator.GetDatabase(input.Region);

                destination.Articles.Add(article);
                await destination.SaveChangesAsync();
            }
            else
            {
                await db.SaveChangesAsync();
            }

            await cache.Remove(id);

            return Ok(article);
        }

        return NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        foreach (var region in coordinator.GetRegions())
        {
            await using var db = coordinator.GetDatabase(region);

            var article = await db.Articles.FindAsync(id);

            if (article is null)
                continue;

            db.Articles.Remove(article);
            await db.SaveChangesAsync();

            await cache.Remove(id);

            return NoContent();
        }

        return NotFound();
    }

    [HttpGet("instance")]
    public IActionResult Instance()
    {
        return Ok(new
        {
            instance = Environment.GetEnvironmentVariable("INSTANCE_ID")
                ?? "local"
        });
    }
}