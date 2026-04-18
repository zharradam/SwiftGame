using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SwiftGame.API.Hubs;
using SwiftGame.API.Models;
using SwiftGame.API.Settings;
using SwiftGame.Data.Entities;
using SwiftGame.Data.Repositories;
using SwiftGame.Music.Abstractions;

namespace SwiftGame.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private const int BasePoints = 10000;
    private const int MinPoints = 100;

    private readonly IPreviewResolver _previewResolver;
    private readonly ISongRepository _songRepository;
    private readonly ILeaderboardRepository _leaderboardRepository;
    private readonly IGameSessionRepository _gameSessionRepository;
    private readonly GameSettings _gameSettings;
    private readonly IHubContext<LeaderboardHub> _hubContext;

    public GameController(
        IMusicProviderFactory factory,
        ISongRepository songRepository,
        ILeaderboardRepository leaderboardRepository,
        IGameSessionRepository gameSessionRepository,
        IOptions<GameSettings> gameSettings,
        IHubContext<LeaderboardHub> hubContext)
    {
        _previewResolver = factory.CreatePreviewResolver();
        _songRepository = songRepository;
        _leaderboardRepository = leaderboardRepository;
        _gameSessionRepository = gameSessionRepository;
        _gameSettings = gameSettings.Value;
        _hubContext = hubContext;
    }

    // ── GET api/game/config ───────────────────────────────────────────────────

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            QuestionsPerGame = _gameSettings.QuestionsPerGame,
            ImageBaseUrl = _gameSettings.ImageBaseUrl,
            ImageCount = _gameSettings.ImageCount
        });
    }

    // ── GET api/game/round ────────────────────────────────────────────────────

    [HttpGet("round")]
    public async Task<IActionResult> GetRound([FromQuery] string? excludeIds, CancellationToken cancellationToken)
    {
        var excluded = excludeIds?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList() ?? [];

        var songs = await _songRepository.GetRandomSongsAsync(4, excluded, cancellationToken);

        // Fallback — if not enough songs after exclusions, try without exclusions
        if (songs.Count < 4)
            songs = await _songRepository.GetRandomSongsAsync(4, null, cancellationToken);

        if (songs.Count < 4)
            return StatusCode(503, "Not enough songs in the catalogue — please seed the database first.");

        var correct = songs[0];
        var preview = correct.PreviewUrl is not null
            ? new Music.Abstractions.Models.PreviewClip(correct.PreviewUrl, 30, correct.Provider)
            : await _previewResolver.GetPreviewAsync(correct.ProviderId, cancellationToken);

        if (preview is null)
            return StatusCode(503, "Could not resolve a preview for this track.");

        var choices = songs
            .Select(s => s.Title)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        return Ok(new RoundResponse
        {
            SongId = correct.ProviderId,
            SongDbId = correct.Id,
            Provider = correct.Provider,
            PreviewUrl = preview.Url,
            StartAt = Random.Shared.Next(10, 20),
            Choices = choices
        });
    }

    // ── POST api/game/submit ──────────────────────────────────────────────────

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitAnswer(
        [FromBody] SubmitAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var song = await _songRepository.GetByProviderIdAsync(
            request.SongId,
            request.Provider,
            cancellationToken);

        if (song is null)
            return NotFound("Song not found — has the round expired?");

        var isCorrect = string.Equals(
            request.SelectedTitle,
            song.Title,
            StringComparison.OrdinalIgnoreCase);

        var pointsEarned = isCorrect ? CalculatePoints(request.ResponseTimeMs) : 0;
        var playerId = Guid.TryParse(request.PlayerId, out var pid) ? pid : Guid.Empty;
        var sessionId = Guid.TryParse(request.SessionId, out var sid) ? sid : Guid.Empty;


        await _leaderboardRepository.AddScoreAsync(new Score
        {
            SongId = song.Id,
            PlayerId = playerId,
            GameSessionId = sessionId,
            PointsEarned = pointsEarned,
            ResponseTimeMs = request.ResponseTimeMs,
            IsCorrect = isCorrect,
            PlayedAt = DateTime.UtcNow
        }, cancellationToken);

        return Ok(new SubmitAnswerResponse
        {
            IsCorrect = isCorrect,
            PointsEarned = pointsEarned,
            CorrectTitle = song.Title,
            ResponseTimeMs = request.ResponseTimeMs
        });
    }

    // ── POST api/game/session/start ───────────────────────────────────────────

    [HttpPost("session/start")]
    public async Task<IActionResult> StartSession(
        [FromBody] StartSessionRequest request,
        CancellationToken cancellationToken)
    {
        // Use the player ID from the request, fall back to guest if not provided
        var playerId = Guid.TryParse(request.PlayerId, out var pid) ? pid : Guid.Empty;

        var session = await _gameSessionRepository.CreateAsync(playerId, cancellationToken);

        return Ok(new StartSessionResponse
        {
            SessionId = session.Id,
            PlayerId = playerId
        });
    }

    // ── POST api/game/session/end ─────────────────────────────────────────────

    [HttpPost("session/end")]
    public async Task<IActionResult> EndSession(
        [FromBody] EndSessionRequest request,
        CancellationToken cancellationToken)
    {
        await _gameSessionRepository.CompleteAsync(request.SessionId, cancellationToken);

        var rank = await _gameSessionRepository.GetSessionRankAsync(
            request.SessionId, cancellationToken);

        var totalSessions = await _gameSessionRepository.GetTopSessionsAsync(
            int.MaxValue, cancellationToken);

        await _hubContext.Clients.Group("leaderboard")
            .SendAsync("LeaderboardUpdated", cancellationToken);

        return Ok(new
        {
            Rank = rank,
            TotalPlayers = totalSessions.Count
        });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static int CalculatePoints(int responseTimeMs)
    {
        var seconds = responseTimeMs / 1000.0;
        var points = (int)(BasePoints * Math.Exp(-0.05 * seconds));
        return Math.Max(points, MinPoints);
    }
}