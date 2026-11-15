
using Microsoft.AspNetCore.Mvc;
using ProfanityService.Models;
using System.Text.RegularExpressions;

namespace ProfanityService.Controllers;

[ApiController]
[Route("api/profanity")]
public class ProfanityController : ControllerBase
{
    private static readonly string[] ForbiddenWords =
    [
        "badword",
        "offensiveword"
    ];

    [HttpPost("check")]
    public IActionResult Check(CheckTextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text cannot be empty.");

        var containsForbiddenWord = ForbiddenWords.Any(word =>
            Regex.IsMatch(
                request.Text,
                $@"\b{Regex.Escape(word)}\b",
                RegexOptions.IgnoreCase));

        return Ok(new
        {
            IsClean = !containsForbiddenWord
        });
    }
}
