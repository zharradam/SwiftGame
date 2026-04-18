namespace SwiftGame.API.Models;

public class SubmitAnswerRequest
{
    public string SongId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string SelectedTitle { get; set; } = string.Empty;
    public int ResponseTimeMs { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
}