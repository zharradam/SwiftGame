using Microsoft.AspNetCore.Mvc;
using SwiftGame.Data.Repositories;

namespace SwiftGame.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController(IGameSessionRepository sessions) : ControllerBase
{
    // period: "all" (default) or "today" (UTC day)
    private static DateTime? Since(string? period)
        => period == "today" ? DateTime.UtcNow.Date : null;

    [HttpGet("top")]
    public async Task<IActionResult> GetTopScores(
        [FromQuery] int count = 10,
        [FromQuery] string? period = null,
        CancellationToken cancellationToken = default)
    {
        var top = await sessions.GetTopSessionsAsync(count, Since(period), cancellationToken);
        return Ok(top.Select(ToEntry));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyBest(
        [FromQuery] Guid playerId,
        [FromQuery] string? period = null,
        CancellationToken cancellationToken = default)
    {
        if (playerId == Guid.Empty) return NoContent();

        var best = await sessions.GetBestSessionAsync(playerId, Since(period), cancellationToken);
        if (best is null) return NoContent();

        var rank = await sessions.GetSessionRankAsync(best.Id, Since(period), cancellationToken);
        return Ok(new { rank, entry = ToEntry(best) });
    }

    private static object ToEntry(GameSession s) => new
    {
        id = s.Id,
        playerName = s.Player.Username,
        songTitle = s.Scores
            .OrderByDescending(sc => sc.PointsEarned)
            .FirstOrDefault()?.Song.Title ?? "Unknown",
        pointsEarned = s.TotalPoints,
        responseTimeMs = s.Scores.Any()
            ? (int)s.Scores.Average(sc => sc.ResponseTimeMs)
            : 0,
        playedAt = s.CompletedAt
    };
}
