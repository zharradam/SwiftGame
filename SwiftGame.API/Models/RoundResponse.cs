namespace SwiftGame.API.Models;

public class RoundResponse
{
    public string SongId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string PreviewUrl { get; set; } = string.Empty;
    public int StartAt { get; set; }  // seconds offset into the 30s clip
    public List<string> Choices { get; set; } = new();
}