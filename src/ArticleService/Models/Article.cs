namespace ArticleService.Models;

public class Article
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Title { get; set; } = "";
	public string Content { get; set; } = "";
	public DateTime PublishedUtc { get; set; }
	public string Region { get; set; } = "Global";
}