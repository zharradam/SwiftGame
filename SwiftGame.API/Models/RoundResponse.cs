namespace SwiftGame.API.Models;

public class RoundResponse
{
    public string SongId { get; set; } = string.Empty;
    public Guid SongDbId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string PreviewUrl { get; set; } = string.Empty;
    public int StartAt { get; set; }
    public List<string> Choices { get; set; } = [];
}