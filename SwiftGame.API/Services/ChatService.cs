using System.Collections.Concurrent;
using SwiftGame.API.Models.Chat;

namespace SwiftGame.API.Services;

public interface IChatService
{
    IReadOnlyList<ChatMessage> GetRecentMessages();
    (bool allowed, string filtered) ProcessMessage(string username, string message, bool isGuest, bool isBanned);
    ChatMessage AddMessage(string username, string message, bool isGuest, bool isSystem = false);
    bool IsRateLimited(string username);
    void DeleteMessage(string messageId);
    void DeleteMessagesByUser(string username);
    void ClearMessages();
}

public class ChatService : IChatService
{
    private const int MaxMessages = 100;
    private const int MaxMessageLength = 200;

    private readonly ConcurrentQueue<ChatMessage> _messages = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastMessageTime = new();
    private readonly ProfanityFilter.ProfanityFilter _profanityFilter = new();

    public IReadOnlyList<ChatMessage> GetRecentMessages()
        => _messages.ToArray();

    public bool IsRateLimited(string username)
    {
        if (_lastMessageTime.TryGetValue(username, out var lastTime))
        {
            if (DateTime.UtcNow - lastTime < TimeSpan.FromSeconds(1))
                return true;
        }
        _lastMessageTime[username] = DateTime.UtcNow;
        return false;
    }

    public (bool allowed, string filtered) ProcessMessage(
        string username, string message, bool isGuest, bool isBanned)
    {
        if (isGuest) return (false, string.Empty);
        if (isBanned) return (false, string.Empty);

        if (string.IsNullOrWhiteSpace(message))
            return (false, string.Empty);

        var trimmed = message.Trim();
        if (trimmed.Length > MaxMessageLength)
            trimmed = trimmed[..MaxMessageLength];

        var filtered = _profanityFilter.CensorString(trimmed, '*');
        return (true, filtered);
    }

    public ChatMessage AddMessage(string username, string message, bool isGuest, bool isSystem = false)
    {
        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            Message = message,
            IsGuest = isGuest,
            IsSystem = isSystem,
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        _messages.Enqueue(chatMessage);

        while (_messages.Count > MaxMessages)
            _messages.TryDequeue(out _);

        return chatMessage;
    }

    public void DeleteMessage(string messageId)
    {
        var messages = _messages.ToArray();
        while (_messages.TryDequeue(out _)) { }
        foreach (var msg in messages.Where(m => m.Id != messageId))
            _messages.Enqueue(msg);
    }

    public void DeleteMessagesByUser(string username)
    {
        var messages = _messages.ToArray();
        while (_messages.TryDequeue(out _)) { }
        foreach (var msg in messages.Where(m => m.Username != username))
            _messages.Enqueue(msg);
    }

    public void ClearMessages()
    {
        while (_messages.TryDequeue(out _)) { }
    }
}