
namespace CommentService.Models;

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArticleId { get; set; }

    public string Text { get; set; } = "";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
