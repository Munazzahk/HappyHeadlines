
using CommentService.Data;
using CommentService.Models;
using CommentService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommentService.Controllers;

[ApiController]
[Route("api/comments")]
public class CommentsController(
    CommentDbContext db,
    CommentCache cache,
    ProfanityClient profanityClient,
    ILogger<CommentsController> logger) : ControllerBase
{
    [HttpGet("article/{articleId:guid}")]
    public async Task<IActionResult> GetForArticle(Guid articleId)
    {
        var cached = await cache.Get(articleId);

        if (cached is not null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cached);
        }

        var comments = await db.Comments
            .Where(c => c.ArticleId == articleId)
            .OrderBy(c => c.CreatedUtc)
            .ToListAsync();

        await cache.Set(articleId, comments);

        Response.Headers["X-Cache"] = "MISS";
        return Ok(comments);
    }


    [HttpPost]
    public async Task<IActionResult> Create(Comment input)
    {
        logger.LogInformation("A new comment is being submitted.");
        bool isClean;

        try
        {
            isClean = await profanityClient.IsCleanAsync(input.Text);
        }
        catch (Exception ex) when (
            ex is CircuitOpenException ||
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            logger.LogError(ex, "Failed to validate comment for profanity.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Comment validation is temporarily unavailable. Please try again later."); // Return 503 Service Unavailable if ProfanityService is down or the circuit breaker is open
        }
        
        if (!isClean)
        {
            logger.LogWarning("Comment rejected due to inappropriate language.");
            return BadRequest("Comment contains inappropriate language.");
        }

        var comment = new Comment
        {
            ArticleId = input.ArticleId,
            Text = input.Text
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        await cache.Remove(comment.ArticleId);
        logger.LogInformation(
        "Comment {CommentId} was successfully created.",
        comment.Id);
        return Created("", comment);
    }


    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var comment = await db.Comments.FindAsync(id);

        if (comment is null)
            return NotFound();

        var articleId = comment.ArticleId;

        db.Comments.Remove(comment);
        await db.SaveChangesAsync();

        await cache.Remove(articleId);

        return NoContent();
    }
}
