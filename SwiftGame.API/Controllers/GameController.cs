using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SwiftGame.API.Hubs;
using SwiftGame.API.Models;
using SwiftGame.API.Services;
using SwiftGame.API.Settings;
using SwiftGame.Data.Entities;
using SwiftGame.Data.Repositories;
using SwiftGame.Music.Abstractions;
using Microsoft.EntityFrameworkCore;
using SwiftGame.Data;

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
    private readonly IHubContext<ChatHub> _chatContext;
    private readonly IAuditService _audit;
    private readonly SwiftGameDbContext _db;

    // Full-catalogue modes share the leaderboards; album and era modes are practice
    public static readonly string[] RankedModes = ["classic", "daily", "challenge"];
    private static readonly string[] AllModes = ["classic", "daily", "challenge", "album", "eras"];

    public GameController(
        IMusicProviderFactory factory,
        ISongRepository songRepository,
        ILeaderboardRepository leaderboardRepository,
        IGameSessionRepository gameSessionRepository,
        IOptions<GameSettings> gameSettings,
        IHubContext<LeaderboardHub> hubContext,
        IHubContext<ChatHub> chatHubContext,
        IAuditService audit,
        SwiftGameDbContext db)
    {
        _previewResolver = factory.CreatePreviewResolver();
        _songRepository = songRepository;
        _leaderboardRepository = leaderboardRepository;
        _gameSessionRepository = gameSessionRepository;
        _gameSettings = gameSettings.Value;
        _hubContext = hubContext;
        _chatContext = chatHubContext;
        _audit = audit;
        _db = db;
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
    public async Task<IActionResult> GetRound(
        [FromQuery] string? excludeIds,
        [FromQuery] Guid? songId,
        [FromQuery] string? albums,
        [FromQuery] string? kind,
        CancellationToken cancellationToken)
    {
        var excluded = ParseIds(excludeIds);
        var albumIds = ParseIds(albums);

        // Daily and challenge rounds name the song; the rest are random from the pool
        Song? correct = null;
        if (songId is not null)
        {
            correct = await _songRepository.GetByIdAsync(songId.Value, cancellationToken);
            if (correct is null) return NotFound("That song is no longer available.");
            excluded.Add(correct.Id);
        }

        var needed = correct is null ? 4 : 3;
        var pool = await _songRepository.GetRandomSongsAsync(needed + 6, excluded, cancellationToken, albumIds);
        if (pool.Count < needed)
            pool = await _songRepository.GetRandomSongsAsync(needed + 6, correct is null ? null : [correct.Id], cancellationToken, albumIds);
        // A small album may not have 3 other songs: borrow the rest from the full catalogue
        if (pool.Count < needed && albumIds.Count > 0)
            pool = [.. pool, .. await _songRepository.GetRandomSongsAsync(needed + 6, [.. excluded, .. pool.Select(p => p.Id)], cancellationToken)];

        correct ??= pool.FirstOrDefault();
        if (correct is null || pool.Count(x => x.Id != correct.Id) < 3)
            return StatusCode(503, "Not enough songs in the catalogue — please seed the database first.");

        var preview = correct.PreviewUrl is not null
            ? new Music.Abstractions.Models.PreviewClip(correct.PreviewUrl, 30, correct.Provider)
            : await _previewResolver.GetPreviewAsync(correct.ProviderId, cancellationToken);

        if (preview is null)
            return StatusCode(503, "Could not resolve a preview for this track.");

        List<string> choices;
        if (kind == "era")
        {
            // Name the era: the right one plus three others
            var right = Eras.Of(correct.Album)?.Label ?? correct.Album;
            choices = Eras.All.Select(e => e.Label).Where(l => l != right)
                .OrderBy(_ => Random.Shared.Next()).Take(3).Append(right)
                .OrderBy(_ => Random.Shared.Next()).ToList();
        }
        else
        {
            var title = SongRepository.NormaliseTitle(correct.Title);
            choices = pool.Where(x => x.Id != correct.Id && SongRepository.NormaliseTitle(x.Title) != title)
                .GroupBy(x => SongRepository.NormaliseTitle(x.Title)).Select(g => g.First().Title)
                .Take(3).Append(correct.Title)
                .OrderBy(_ => Random.Shared.Next()).ToList();
            if (choices.Count < 4)
                return StatusCode(503, "Not enough songs in the catalogue — please seed the database first.");
        }

        return Ok(new RoundResponse
        {
            SongId = correct.ProviderId,
            SongDbId = correct.Id,
            Provider = correct.Provider,
            PreviewUrl = preview.Url,
            StartAt = Random.Shared.Next(10, 20),
            Choices = choices,
            Kind = kind == "era" ? "era" : "song"
        });
    }

    // ── GET api/game/set?mode=daily | mode=challenge&from={sessionId} ─────────

    [HttpGet("set")]
    public async Task<IActionResult> GetSet([FromQuery] string mode, [FromQuery] Guid? from, CancellationToken cancellationToken)
    {
        if (mode == "daily")
        {
            var today = Clock.Today;
            var pool = (await _songRepository.GetPlayablePoolAsync(cancellationToken)).ToList();
            var seeded = new Random(today.Year * 10000 + today.Month * 100 + today.Day);
            var ids = pool.OrderBy(_ => seeded.Next()).Take(_gameSettings.QuestionsPerGame).ToList();
            return Ok(new { mode, date = today.ToString("yyyy-MM-dd"), songIds = ids });
        }

        if (mode == "challenge" && from is not null)
        {
            var source = await _db.GameSessions.AsNoTracking()
                .Include(g => g.Player)
                .Include(g => g.Scores)
                .FirstOrDefaultAsync(g => g.Id == from && g.IsComplete, cancellationToken);
            if (source is null) return NotFound(new { message = "That challenge link has expired or the game wasn't finished." });

            var rounds = source.Scores.OrderBy(x => x.PlayedAt).ToList();
            return Ok(new
            {
                mode,
                from = source.Id,
                challenger = source.PlayerId == Guid.Empty ? "A friend" : source.Player.Username,
                challengerScore = source.TotalPoints,
                challengerRounds = rounds.Select(r => new { r.IsCorrect, r.PointsEarned }),
                songIds = rounds.Select(r => r.SongId)
            });
        }

        return BadRequest();
    }

    // ── GET api/game/albums (for Album mode) ──────────────────────────────────

    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums(CancellationToken cancellationToken)
    {
        var albums = await _db.Albums.AsNoTracking()
            .Select(a => new { a.Id, a.Name, Songs = a.Songs.Count(x => x.PreviewUrl != null) })
            .Where(a => a.Songs >= 4)
            .ToListAsync(cancellationToken);

        return Ok(albums
            .Select(a => new { a.Id, a.Name, a.Songs, Era = Eras.Of(a.Name)?.Key })
            .OrderBy(a => a.Era == null ? 99 : Eras.All.ToList().FindIndex(e => e.Key == a.Era))
            .ThenBy(a => a.Name));
    }

    private static List<Guid> ParseIds(string? csv) => csv?
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
        .Where(g => g.HasValue)
        .Select(g => g!.Value)
        .Take(200)
        .ToList() ?? [];

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

        var sessionId = Guid.TryParse(request.SessionId, out var sid) ? sid : Guid.Empty;
        var mode = await _db.GameSessions.Where(g => g.Id == sessionId).Select(g => g.Mode).FirstOrDefaultAsync(cancellationToken);
        var correctEra = Eras.Of(song.Album)?.Label ?? song.Album;

        var isCorrect = mode == "eras"
            ? string.Equals(request.SelectedTitle, correctEra, StringComparison.OrdinalIgnoreCase)
            : string.Equals(request.SelectedTitle, song.Title, StringComparison.OrdinalIgnoreCase);

        var pointsEarned = isCorrect ? CalculatePoints(request.ResponseTimeMs) : 0;
        var playerId = Guid.TryParse(request.PlayerId, out var pid) ? pid : Guid.Empty;

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
            ResponseTimeMs = request.ResponseTimeMs,
            Album = song.Album,
            AlbumArt = song.AlbumArt,
            CorrectEra = correctEra
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

        var mode = AllModes.Contains(request.Mode) ? request.Mode! : "classic";
        Guid? challengeOf = mode == "challenge" && Guid.TryParse(request.ChallengeOf, out var c) ? c : null;

        var session = await _gameSessionRepository.CreateAsync(playerId, mode, challengeOf, cancellationToken);
        await _audit.LogAsync("game_start", playerId == Guid.Empty ? null : playerId, playerId == Guid.Empty ? "Guest" : null, mode == "classic" ? null : mode);

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

        var session = await _gameSessionRepository.GetByIdAsync(request.SessionId, cancellationToken);

        // Daily games rank against today's daily games; practice modes aren't ranked
        var sessionMode = session?.Mode ?? "classic";
        var ranked = RankedModes.Contains(sessionMode);
        DateTime? since = sessionMode == "daily" ? Clock.TodayStartUtc : null;
        string[] pool = sessionMode == "daily" ? ["daily"] : RankedModes;

        var rank = ranked ? await _gameSessionRepository.GetSessionRankAsync(request.SessionId, since, pool, cancellationToken) : 0;
        var totalSessions = ranked ? await _gameSessionRepository.CountCompletedAsync(since, pool, cancellationToken) : 0;

        if (session is not null)
        {
            var name = session.PlayerId == Guid.Empty ? "Guest" : session.Player?.Username;
            await _audit.LogAsync("game_end", session.PlayerId == Guid.Empty ? null : session.PlayerId, name,
                $"{session.TotalPoints:N0} pts · {session.Scores.Count(x => x.IsCorrect)}/{session.QuestionsCount} right" + (ranked ? $" · #{rank}" : "") + (sessionMode == "classic" ? "" : $" · {sessionMode}"));
        }

        await _hubContext.Clients.Group("leaderboard")
            .SendAsync("LeaderboardUpdated", cancellationToken);

        if (session is not null && session.PlayerId != Guid.Empty && ranked)
        {
            await _chatContext.Clients.All.SendAsync("ScoreAnnounced",
                new { playerName = session.Player.Username, points = session.TotalPoints, rank },
                cancellationToken);
        }

        return Ok(new
        {
            Rank = rank,
            TotalPlayers = totalSessions,
            Mode = sessionMode
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