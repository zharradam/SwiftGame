namespace SwiftGame.API.Settings;

public class GameSettings
{
    public int QuestionsPerGame { get; set; } = 10;
    public string ImageBaseUrl { get; set; } = string.Empty;
    public int ImageCount { get; set; } = 15;
}