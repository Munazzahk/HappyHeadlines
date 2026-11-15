
using System.Net.Http.Json;
using CommentService.Models;

namespace CommentService.Services;

public class ProfanityClient(
    HttpClient httpClient,
    ProfanityCircuitBreaker circuitBreaker)
{
    public Task<bool> IsCleanAsync(string text)
    {
        return circuitBreaker.ExecuteAsync(async () =>
        {
            var response = await httpClient.PostAsJsonAsync(
                "api/profanity/check",
                new CheckTextRequest { Text = text });

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<CheckTextResponse>();

            if (result is null)
                throw new HttpRequestException(
                    "Invalid response from ProfanityService.");

            return result.IsClean;
        });
    }
}

public class CheckTextRequest
{
    public string Text { get; set; } = "";
}

public class CheckTextResponse
{
    public bool IsClean { get; set; }
}
