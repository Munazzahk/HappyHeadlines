using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data;

public class ArticleDatabaseCoordinator
{
    private readonly IConfiguration _configuration;

    public ArticleDatabaseCoordinator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ArticleDbContext GetDatabase(string region)
    {
        var connectionName = region.ToLowerInvariant() switch
        {
            "europe" => "ArticlesEurope",
            "global" => "ArticlesGlobal",
            _ => throw new ArgumentException(
                $"Unknown region: {region}")
        };

        var connectionString =
            _configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException(
                $"Connection string {connectionName} was not found.");

        var options = new DbContextOptionsBuilder<ArticleDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ArticleDbContext(options);
    }

    public string[] GetRegions()
    {
        return ["Global", "Europe"];
    }
}