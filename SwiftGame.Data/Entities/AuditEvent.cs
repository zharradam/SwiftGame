namespace SwiftGame.Data.Entities;

// One row per thing that happened: a visit, a login, a game, an admin action.
// Deliberately no IP address or user-agent string (the audience is mostly kids).
public class AuditEvent
{
    public long Id { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty;   // visit, register, login, logout, game_start, game_end, chat, admin
    public Guid? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public string? VisitorId { get; set; }              // random id kept in the browser
    public string? Source { get; set; }                 // visits only: Google, Direct, Shared link...
    public string? Device { get; set; }                 // visits only: phone, tablet, desktop
    public string? Detail { get; set; }
}
