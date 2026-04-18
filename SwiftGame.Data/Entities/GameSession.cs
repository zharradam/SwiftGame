using SwiftGame.Data.Entities;
using System.ComponentModel.DataAnnotations.Schema;

public class GameSession
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public int TotalPoints { get; set; }
    public int QuestionsCount { get; set; }
    public bool IsComplete { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Player Player { get; set; } = null!;
    public ICollection<Score> Scores { get; set; } = new List<Score>();
}