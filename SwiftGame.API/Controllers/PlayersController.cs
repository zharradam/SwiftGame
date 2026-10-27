using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwiftGame.API.Services;
using SwiftGame.Data;

namespace SwiftGame.API.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController(SwiftGameDbContext db) : ControllerBase
{
    // The avatars players can pick from (kept server-side so nobody stores arbitrary text)
    public static readonly string[] Avatars =
        ["🐍", "🦋", "🧣", "🌲", "🍂", "🌙", "🖋️", "✨", "🎸", "🕶️", "💃", "🐈", "💜", "🌈", "🎤", "⭐"];

    private const int BeadAt = 10;   // correct answers from an era to earn its bead

    public record ProfileRequest(string? Avatar, string? FavouriteEra);

    // ── PUT /api/players/me/profile ───────────────────────────────────────────

    [Authorize]
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        if (req.Avatar is not null && !Avatars.Contains(req.Avatar)) return BadRequest(new { message = "Pick one of the listed avatars." });
        if (req.FavouriteEra is not null && !Eras.IsKey(req.FavouriteEra)) return BadRequest(new { message = "Pick one of the listed eras." });

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (player is null) return NotFound();

        player.Avatar = req.Avatar;
        player.FavouriteEra = req.FavouriteEra;
        await db.SaveChangesAsync(ct);
        return Ok(new { player.Avatar, player.FavouriteEra });
    }

    // ── GET /api/players/{id}/progress ────────────────────────────────────────
    // Public on purpose: it's the same information the leaderboard already shows.

    [HttpGet("{id:guid}/progress")]
    public async Task<IActionResult> Progress(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return NoContent();
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (player is null) return NotFound();

        var sessions = await db.GameSessions.AsNoTracking()
            .Where(g => g.PlayerId == id && g.IsComplete)
            .Select(g => new { g.TotalPoints, g.Mode, g.CompletedAt, Correct = g.Scores.Count(s => s.IsCorrect), g.QuestionsCount })
            .ToListAsync(ct);

        // Daily streak: consecutive local days with a finished daily, ending today or yesterday
        var dailyDays = sessions.Where(s => s.Mode == "daily" && s.CompletedAt != null)
            .Select(s => Clock.Local(s.CompletedAt!.Value).Date).ToHashSet();
        var today = Clock.Today;
        var day = dailyDays.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (dailyDays.Contains(day)) { streak++; day = day.AddDays(-1); }

        // Bracelet: one bead per era with enough correct answers, gold beads for perfect games
        var correctAlbums = await db.Scores.AsNoTracking()
            .Where(s => s.PlayerId == id && s.IsCorrect)
            .Select(s => s.Song.Album)
            .ToListAsync(ct);
        var perEra = correctAlbums.Select(Eras.Of).Where(e => e is not null)
            .GroupBy(e => e!.Key).ToDictionary(g => g.Key, g => g.Count());

        return Ok(new
        {
            player.Avatar,
            player.FavouriteEra,
            totalPoints = sessions.Sum(s => (long)s.TotalPoints),
            games = sessions.Count,
            streak,
            playedDailyToday = dailyDays.Contains(today),
            beads = Eras.All.Select(e => new { e.Key, e.Label, correct = perEra.GetValueOrDefault(e.Key), earned = perEra.GetValueOrDefault(e.Key) >= BeadAt }),
            beadAt = BeadAt,
            goldBeads = sessions.Count(s => s.QuestionsCount >= 10 && s.Correct == s.QuestionsCount)
        });
    }

    [HttpGet("avatars")]
    public IActionResult GetAvatars() => Ok(new { avatars = Avatars, eras = Eras.All });
}
