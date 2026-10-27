using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwiftGame.API.Services;
using SwiftGame.Data;

namespace SwiftGame.API.Controllers;

// Admin-only audit trail and usage stats.
[ApiController]
[Route("api/admin")]
[Authorize]
public class InsightsController(SwiftGameDbContext db) : ControllerBase
{
    private bool IsAdmin => User.FindFirst("isAdmin")?.Value == "true";

    private static DateTime Local(DateTime utc) => Clock.Local(utc);

    // ── GET /api/admin/audit ──────────────────────────────────────────────────

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(
        [FromQuery] string? type, [FromQuery] string? player,
        [FromQuery] long? before, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (!IsAdmin) return Forbid();
        take = Math.Clamp(take, 1, 200);

        var q = db.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(e => e.Type == type);
        if (!string.IsNullOrWhiteSpace(player)) q = q.Where(e => e.PlayerName != null && e.PlayerName.Contains(player));
        if (before is not null) q = q.Where(e => e.Id < before);

        var rows = await q.OrderByDescending(e => e.Id).Take(take).ToListAsync(ct);
        return Ok(rows.Select(e => new
        {
            e.Id, e.At, e.Type, e.PlayerName, e.Source, e.Device, e.Detail,
            Visitor = e.VisitorId == null ? null : e.VisitorId[..8]
        }));
    }

    // ── GET /api/admin/stats?days=30 (0 = all time) ───────────────────────────

    [HttpGet("stats")]
    public async Task<IActionResult> Stats([FromQuery] int days = 30, CancellationToken ct = default)
    {
        if (!IsAdmin) return Forbid();

        var now = DateTime.UtcNow;
        var first = await db.AuditEvents.MinAsync(e => (DateTime?)e.At, ct) ?? now;
        var from = days > 0 ? now.AddDays(-days) : first;
        var prevFrom = days > 0 ? from.AddDays(-days) : (DateTime?)null;

        // Stats are about other people: drop admins, and any browser an admin has ever used
        var admins = await db.Players.Where(p => p.IsAdmin).Select(p => new { p.Id, p.Username }).ToListAsync(ct);
        var adminIds = admins.Select(a => (Guid?)a.Id).ToList();
        var adminNames = admins.Select(a => a.Username).ToList();
        var adminVisitors = await db.AuditEvents.AsNoTracking()
            .Where(e => e.VisitorId != null && (adminIds.Contains(e.PlayerId) || (e.PlayerName != null && adminNames.Contains(e.PlayerName))))
            .Select(e => e.VisitorId).Distinct().ToListAsync(ct);

        var events = await db.AuditEvents.AsNoTracking()
            .Where(e => e.At >= (prevFrom ?? from))
            .Where(e => !adminIds.Contains(e.PlayerId))
            .Where(e => e.PlayerName == null || !adminNames.Contains(e.PlayerName))
            .Where(e => e.VisitorId == null || !adminVisitors.Contains(e.VisitorId))
            .Select(e => new { e.At, e.Type, e.PlayerName, e.VisitorId, e.Source, e.Device })
            .ToListAsync(ct);

        var cur = events.Where(e => e.At >= from).ToList();
        var prev = events.Where(e => e.At < from).ToList();

        // Visitors seen for the first time ever in this period
        var visitorIds = cur.Where(e => e.VisitorId != null).Select(e => e.VisitorId!).Distinct().ToList();
        var seenBefore = await db.AuditEvents.AsNoTracking()
            .Where(e => e.At < from && e.VisitorId != null && visitorIds.Contains(e.VisitorId))
            .Select(e => e.VisitorId).Distinct().CountAsync(ct);

        var visits = cur.Where(e => e.Type == "visit").ToList();
        var prevVisits = prev.Where(e => e.Type == "visit").ToList();

        var totals = new
        {
            visits = visits.Count,
            visitors = visits.Select(e => e.VisitorId).Distinct().Count(),
            newVisitors = visitorIds.Count - seenBefore,
            games = cur.Count(e => e.Type == "game_end"),
            phonePercent = visits.Count == 0 ? 0 : (int)Math.Round(100.0 * visits.Count(e => e.Device == "phone") / visits.Count)
        };
        var previous = prevFrom is null ? null : new
        {
            visits = prevVisits.Count,
            visitors = prevVisits.Select(e => e.VisitorId).Distinct().Count(),
            games = prev.Count(e => e.Type == "game_end"),
            phonePercent = prevVisits.Count == 0 ? 0 : (int)Math.Round(100.0 * prevVisits.Count(e => e.Device == "phone") / prevVisits.Count)
        };

        // Visits per local day across the whole window, zero-filled
        var startDay = Local(from).Date;
        var endDay = Local(now).Date;
        var perDayMap = visits.GroupBy(e => Local(e.At).Date).ToDictionary(g => g.Key, g => g.Count());
        var perDay = new List<object>();
        for (var d = startDay; d <= endDay; d = d.AddDays(1))
            perDay.Add(new { date = d.ToString("yyyy-MM-dd"), count = perDayMap.GetValueOrDefault(d) });

        var byHour = Enumerable.Range(0, 24).Select(h => visits.Count(e => Local(e.At).Hour == h)).ToArray();
        // Monday first
        var byWeekday = Enumerable.Range(0, 7).Select(i => visits.Count(e => ((int)Local(e.At).DayOfWeek + 6) % 7 == i)).ToArray();

        var actions = cur.GroupBy(e => e.Type)
            .Select(g => new { label = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).ToList();

        var sources = visits.GroupBy(e => e.Source ?? "Direct")
            .Select(g => new { label = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(10).ToList();

        var topPlayers = cur.Where(e => e.Type == "game_end" && e.PlayerName != null)
            .GroupBy(e => e.PlayerName!)
            .Select(g => new { label = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(10).ToList();

        // Song difficulty from the scores themselves (covers games before auditing existed)
        var adminGuids = admins.Select(a => a.Id).ToList();
        var scoreQ = db.Scores.AsNoTracking().Where(s => s.PlayedAt >= from && !adminGuids.Contains(s.PlayerId));
        var hardest = await scoreQ
            .GroupBy(s => new { s.Song.Title, s.Song.Album })
            .Select(g => new { g.Key.Title, g.Key.Album, plays = g.Count(), misses = g.Count(s => !s.IsCorrect) })
            .Where(x => x.plays >= 2)
            .OrderByDescending(x => (double)x.misses / x.plays).ThenByDescending(x => x.plays)
            .Take(10).ToListAsync(ct);

        var albums = await scoreQ
            .GroupBy(s => s.Song.Album)
            .Select(g => new { album = g.Key, plays = g.Count(), correct = g.Count(s => s.IsCorrect) })
            .ToListAsync(ct);

        return Ok(new
        {
            from = Local(from).ToString("yyyy-MM-dd"),
            to = Local(now).ToString("yyyy-MM-dd"),
            totals, previous, perDay, byHour, byWeekday,
            actions, sources, topPlayers,
            hardest = hardest.Select(h => new { label = h.Title, album = h.Album, h.plays, h.misses }),
            albums
        });
    }
}
