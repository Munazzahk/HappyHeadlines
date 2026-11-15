
using CommentService.Models;
using Microsoft.EntityFrameworkCore;

namespace CommentService.Data;

public class CommentDbContext(
    DbContextOptions<CommentDbContext> options) : DbContext(options)
{
    public DbSet<Comment> Comments => Set<Comment>();
}
