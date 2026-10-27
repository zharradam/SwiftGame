using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SwiftGame.Data;
using SwiftGame.Data.Entities;

namespace SwiftGame.API.Services;

public interface IAuditService
{
    Task LogAsync(string type, Guid? playerId = null, string? playerName = null, string? detail = null);
    Task LogVisitAsync(string visitorId, string? source, string? device);
}

public class AuditService(SwiftGameDbContext db, IHttpContextAccessor http, ILogger<AuditService> logger) : IAuditService
{
    public const string VisitorHeader = "X-Visitor-Id";

    // Auditing must never break the action being audited.
    public async Task LogAsync(string type, Guid? playerId = null, string? playerName = null, string? detail = null)
    {
        try
        {
            var ctx = http.HttpContext;
            playerName ??= ctx?.User.FindFirstValue("username");
            if (playerId is null && Guid.TryParse(ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                playerId = id;
            if (playerId == Guid.Empty) playerId = null;

            db.AuditEvents.Add(new AuditEvent
            {
                Type = type,
                PlayerId = playerId,
                PlayerName = Clip(playerName, 50),
                VisitorId = Clip(ctx?.Request.Headers[VisitorHeader].ToString(), 36),
                Detail = Clip(detail, 200)
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit write failed for {Type}", type);
        }
    }

    public async Task LogVisitAsync(string visitorId, string? source, string? device)
    {
        try
        {
            // One visit per visitor per 30 minutes: reloads and tab switches aren't new visits
            var since = DateTime.UtcNow.AddMinutes(-30);
            if (await db.AuditEvents.AnyAsync(e => e.Type == "visit" && e.VisitorId == visitorId && e.At >= since))
                return;

            var ctx = http.HttpContext;
            Guid.TryParse(ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var pid);
            db.AuditEvents.Add(new AuditEvent
            {
                Type = "visit",
                VisitorId = Clip(visitorId, 36),
                PlayerId = pid == Guid.Empty ? null : pid,
                PlayerName = Clip(ctx?.User.FindFirstValue("username"), 50),
                Source = Clip(source, 60) ?? "Direct",
                Device = device is "phone" or "tablet" or "desktop" ? device : "desktop"
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Visit write failed");
        }
    }

    private static string? Clip(string? s, int max)
        => string.IsNullOrWhiteSpace(s) ? null : s.Length <= max ? s : s[..max];
}
