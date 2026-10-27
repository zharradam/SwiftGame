namespace SwiftGame.API.Models;

public class SubmitAnswerResponse
{
    public bool IsCorrect { get; set; }
    public int PointsEarned { get; set; }
    public string CorrectTitle { get; set; } = string.Empty;
    public int ResponseTimeMs { get; set; }
    public string Album { get; set; } = string.Empty;
    public string AlbumArt { get; set; } = string.Empty;
}