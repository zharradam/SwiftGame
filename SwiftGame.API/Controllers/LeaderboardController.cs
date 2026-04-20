using Microsoft.AspNetCore.Mvc;
using SwiftGame.Data.Repositories;

namespace SwiftGame.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly IGameSessionRepository _gameSessionRepository;

    public LeaderboardController(IGameSessionRepository gameSessionRepository)
    {
        _gameSessionRepository = gameSessionRepository;
    }

    [HttpGet("top")]
    public async Task<IActionResult> GetTopScores(
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _gameSessionRepository.GetTopSessionsAsync(count, cancellationToken);

        var result = sessions.Select(s => new
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
        });

        return Ok(result);
    }
}