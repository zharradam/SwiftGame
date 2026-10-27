using Microsoft.AspNetCore.Mvc;
using SwiftGame.API.Services;
using SwiftGame.Data.Repositories;

namespace SwiftGame.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController(IGameSessionRepository sessions) : ControllerBase
{
    // period: "all" (default), "today" (local day, all ranked games) or "daily" (today's Daily Swiftle)
    private static DateTime? Since(string? period)
        => period is "today" or "daily" ? Clock.TodayStartUtc : null;

    private static string[] Modes(string? period)
        => period == "daily" ? ["daily"] : GameController.RankedModes;

    [HttpGet("top")]
    public async Task<IActionResult> GetTopScores(
        [FromQuery] int count = 10,
        [FromQuery] string? period = null,
        CancellationToken cancellationToken = default)
    {
        var top = await sessions.GetTopSessionsAsync(Math.Clamp(count, 1, 50), Since(period), Modes(period), cancellationToken);
        return Ok(top.Select(ToEntry));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyBest(
        [FromQuery] Guid playerId,
        [FromQuery] string? period = null,
        CancellationToken cancellationToken = default)
    {
        if (playerId == Guid.Empty) return NoContent();

        var best = await sessions.GetBestSessionAsync(playerId, Since(period), Modes(period), cancellationToken);
        if (best is null) return NoContent();

        var rank = await sessions.GetSessionRankAsync(best.Id, Since(period), Modes(period), cancellationToken);
        return Ok(new { rank, entry = ToEntry(best) });
    }

    private static object ToEntry(GameSession s) => new
    {
        id = s.Id,
        playerName = s.Player.Username,
        avatar = s.Player.Avatar,
        era = s.Player.FavouriteEra,
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
