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
    public async Task<IActionResult> GetRound(CancellationToken cancellationToken)
    {
        // Pull 4 random songs directly from our catalogue
        var songs = await _songRepository.GetRandomSongsAsync(4, cancellationToken);

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

        var startAt = Random.Shared.Next(0, 20);

        return Ok(new RoundResponse
        {
            SongId = correct.ProviderId,
            Provider = correct.Provider,
            PreviewUrl = preview.Url,
            StartAt = startAt,
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

        var pointsEarned = isCorrect
            ? CalculatePoints(request.ResponseTimeMs)
            : 0;

        await _leaderboardRepository.AddScoreAsync(new Score
        {
            SongId = song.Id,
            PlayerId = Guid.Empty,
            GameSessionId = request.SessionId,
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

    // ── Private helpers ───────────────────────────────────────────────────────
    private static int CalculatePoints(int responseTimeMs)
    {
        // Exponential decay — fast answers rewarded heavily,
        // curve flattens out for slower answers
        // k = 0.05 gives a nice curve:
        // 1s  = ~9,512 pts
        // 3s  = ~8,607 pts  
        // 5s  = ~7,788 pts
        // 10s = ~6,065 pts
        // 20s = ~3,679 pts
        // 30s = ~2,231 pts
        var seconds = responseTimeMs / 1000.0;
        var points = (int)(BasePoints * Math.Exp(-0.05 * seconds));
        return Math.Max(points, MinPoints);
    }

    [HttpPost("session/start")]
    public async Task<IActionResult> StartSession(CancellationToken cancellationToken)
    {
        var session = await _gameSessionRepository.CreateAsync(Guid.Empty, cancellationToken);
        return Ok(new { sessionId = session.Id });
    }

    [HttpPost("session/end")]
    public async Task<IActionResult> EndSession([FromBody] EndSessionRequest request, CancellationToken cancellationToken)
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
}