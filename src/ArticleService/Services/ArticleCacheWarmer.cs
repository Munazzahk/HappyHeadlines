using ArticleService.Data;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Services;

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