using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data;

public class ArticleDbContext(
    DbContextOptions<ArticleDbContext> options) : DbContext(options)
{
    public DbSet<Article> Articles => Set<Article>();
}