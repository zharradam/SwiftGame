namespace SwiftGame.Data.Entities;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique display name shown on leaderboard and chat.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Unique email used for login.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the password. Null for legacy guest records.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Opaque refresh token stored server-side for silent re-auth.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>UTC expiry of the current refresh token.</summary>
    public DateTime? RefreshTokenExpiry { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Score> Scores { get; set; } = [];
    public ICollection<GameSession> GameSessions { get; set; } = [];
}