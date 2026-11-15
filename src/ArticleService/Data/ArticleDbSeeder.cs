using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data;

public class ArticleDbSeeder
{
    private readonly ArticleDatabaseCoordinator _coordinator;

    public ArticleDbSeeder(ArticleDatabaseCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public async Task InitializeDatabasesAsync(bool seedData)
    {
        foreach (var region in _coordinator.GetRegions())
        {
            await using var db = _coordinator.GetDatabase(region);

            await db.Database.EnsureCreatedAsync();

            if (!seedData || await db.Articles.AnyAsync())
                continue;

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
}