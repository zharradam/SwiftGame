namespace SwiftGame.API.Models.Chat;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public bool IsGuest { get; set; }
    public bool IsSystem { get; set; }
}