namespace SwiftGame.API.Models;

public class StartSessionResponse
{
    public Guid SessionId { get; set; }
    public Guid PlayerId { get; set; }
}