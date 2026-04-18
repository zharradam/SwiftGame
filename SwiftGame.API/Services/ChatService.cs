using System.Collections.Concurrent;
using SwiftGame.API.Models.Chat;
using ProfanityFilter;

namespace SwiftGame.API.Services;

public interface IChatService
{
    IReadOnlyList<ChatMessage> GetRecentMessages();
    (bool allowed, string filtered) ProcessMessage(string username, string message, bool isGuest);
    ChatMessage AddMessage(string username, string message, bool isGuest, bool isSystem = false);
    bool IsRateLimited(string username);
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

    public (bool allowed, string filtered) ProcessMessage(string username, string message, bool isGuest)
    {
        if (isGuest)
            return (false, string.Empty);

        if (string.IsNullOrWhiteSpace(message))
            return (false, string.Empty);

        var trimmed = message.Trim();
        if (trimmed.Length > MaxMessageLength)
            trimmed = trimmed[..MaxMessageLength];

        // CensorString replaces profanity with asterisks
        var filtered = _profanityFilter.CensorString(trimmed);

        return (true, filtered);
    }

    public ChatMessage AddMessage(string username, string message, bool isGuest, bool isSystem = false)
    {
        var chatMessage = new ChatMessage
        {
            Username = username,
            Message = message,
            IsGuest = isGuest,
            IsSystem = isSystem,
            Timestamp = DateTime.UtcNow.ToString("HH:mm")
        };

        _messages.Enqueue(chatMessage);

        while (_messages.Count > MaxMessages)
            _messages.TryDequeue(out _);

        return chatMessage;
    }
}