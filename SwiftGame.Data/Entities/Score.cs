namespace SwiftGame.Data.Entities;

public class Score
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid SongId { get; set; }
    public Guid GameSessionId { get; set; }
    public int PointsEarned { get; set; }
    public int ResponseTimeMs { get; set; }
    public bool IsCorrect { get; set; }
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    public Player Player { get; set; } = null!;
    public Song Song { get; set; } = null!;
    public GameSession GameSession { get; set; } = null!;
}