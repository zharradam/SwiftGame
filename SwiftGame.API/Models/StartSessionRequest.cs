namespace SwiftGame.API.Models;

public class StartSessionRequest
{
    public string? PlayerId { get; set; }
    public string? Mode { get; set; }
    public string? ChallengeOf { get; set; }
}