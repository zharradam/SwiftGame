// SwiftGame.Data/Entities/Player.cs

namespace SwiftGame.Data.Entities;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    public bool IsAdmin { get; set; } = false;
    public bool IsModerator { get; set; } = false;
    public bool IsBanned { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Score> Scores { get; set; } = [];
    public ICollection<GameSession> GameSessions { get; set; } = [];
}