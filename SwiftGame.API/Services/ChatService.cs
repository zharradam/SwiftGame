using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SwiftGame.Data;
using SwiftGame.Data.Entities;

namespace SwiftGame.API.Services;

public interface IChatService
{
    Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync();
    (bool allowed, string filtered) ProcessMessage(string username, string message, bool isGuest, bool isBanned);
    Task<ChatMessage> AddMessageAsync(string username, string message, bool isGuest, bool isSystem = false);
    bool IsRateLimited(string username);
    Task DeleteMessageAsync(string messageId);
    Task DeleteMessagesByUserAsync(string username);
}

// Scoped (one DbContext per request); rate-limit and profanity state are static so they survive across scopes.
public class ChatService(SwiftGameDbContext db) : IChatService
{
    private const int MaxMessages = 100;
    private const int MaxMessageLength = 200;

    private static readonly ConcurrentDictionary<string, DateTime> LastMessageTime = new();
    private static readonly ProfanityFilter.ProfanityFilter Profanity = new();

    public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync()
    {
        var newest = await db.ChatMessages
            .OrderByDescending(m => m.Timestamp)
            .Take(MaxMessages)
            .ToListAsync();
        newest.Reverse();
        return newest;
    }

    public bool IsRateLimited(string username)
    {
        if (LastMessageTime.TryGetValue(username, out var lastTime) &&
            DateTime.UtcNow - lastTime < TimeSpan.FromSeconds(1))
            return true;

        LastMessageTime[username] = DateTime.UtcNow;
        return false;
    }

    public (bool allowed, string filtered) ProcessMessage(
        string username, string message, bool isGuest, bool isBanned)
    {
        if (isGuest || isBanned || string.IsNullOrWhiteSpace(message))
            return (false, string.Empty);

        var trimmed = message.Trim();
        if (trimmed.Length > MaxMessageLength)
            trimmed = trimmed[..MaxMessageLength];

        return (true, Profanity.CensorString(trimmed, '*'));
    }

    public async Task<ChatMessage> AddMessageAsync(string username, string message, bool isGuest, bool isSystem = false)
    {
        var chatMessage = new ChatMessage
        {
            Username = username,
            Message = message,
            IsGuest = isGuest,
            IsSystem = isSystem,
            Timestamp = DateTime.UtcNow
        };

        db.ChatMessages.Add(chatMessage);
        await db.SaveChangesAsync();

        // Keep only the newest MaxMessages rows
        await db.ChatMessages
            .OrderByDescending(m => m.Timestamp)
            .Skip(MaxMessages)
            .ExecuteDeleteAsync();

        return chatMessage;
    }

    public Task DeleteMessageAsync(string messageId) =>
        db.ChatMessages.Where(m => m.Id == messageId).ExecuteDeleteAsync();

    public Task DeleteMessagesByUserAsync(string username) =>
        db.ChatMessages.Where(m => m.Username == username).ExecuteDeleteAsync();
}
