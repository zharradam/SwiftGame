using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using SwiftGame.API.Services;
using System.Security.Claims;

namespace SwiftGame.API.Hubs;

public class ChatHub(IChatService chatService, IAuditService audit) : Hub
{
    // shortcut: in-process presence, fine for one server instance; move to a store if the API ever scales out
    private static readonly ConcurrentDictionary<string, string?> Online = new();

    public override async Task OnConnectedAsync()
    {
        Online[Context.ConnectionId] = Context.User?.FindFirstValue("username");
        var history = await chatService.GetRecentMessagesAsync();
        await Clients.Caller.SendAsync("ChatHistory", history);
        await BroadcastPresence();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Online.TryRemove(Context.ConnectionId, out _);
        await BroadcastPresence();
        await base.OnDisconnectedAsync(exception);
    }

    private static readonly string[] Reactions = ["🔥", "💜", "👏", "😱", "🫶"];
    private static readonly ConcurrentDictionary<string, DateTime> LastReaction = new();

    // Logged-in players can react to someone's announced score with one of a few emoji
    public async Task React(string toPlayer, string emoji)
    {
        var from = Context.User?.FindFirstValue("username");
        if (from is null || Context.User?.FindFirstValue("isBanned") == "true") return;
        if (!Reactions.Contains(emoji) || string.IsNullOrWhiteSpace(toPlayer) || toPlayer.Length > 50) return;
        if (LastReaction.TryGetValue(from, out var last) && DateTime.UtcNow - last < TimeSpan.FromSeconds(2)) return;
        LastReaction[from] = DateTime.UtcNow;

        await Clients.All.SendAsync("Reaction", new { from, to = toPlayer, emoji });
    }

    private Task BroadcastPresence()
    {
        var names = Online.Values.Where(n => n is not null).Distinct().OrderBy(n => n).ToList();
        return Clients.All.SendAsync("Presence", new { count = Online.Count, names });
    }

    public async Task SendMessage(string message)
    {
        var username = Context.User?.FindFirstValue("username");
        var isGuest = username is null;
        var isBanned = Context.User?.FindFirstValue("isBanned") == "true";
        var display = username ?? "Guest";

        if (isBanned)
        {
            await Clients.Caller.SendAsync("ChatError", "You are banned from chat.");
            return;
        }

        if (chatService.IsRateLimited(display))
        {
            await Clients.Caller.SendAsync("ChatError", "You're sending messages too fast.");
            return;
        }

        var (allowed, filtered) = chatService.ProcessMessage(display, message, isGuest, isBanned);

        if (!allowed)
        {
            await Clients.Caller.SendAsync("ChatError", "Guests cannot send messages. Please log in.");
            return;
        }

        var chatMessage = await chatService.AddMessageAsync(display, filtered, isGuest);
        await audit.LogAsync("chat", null, display);
        await Clients.All.SendAsync("ReceiveMessage", chatMessage);
    }

    public async Task DeleteMessage(string messageId)
    {
        var isAdmin = Context.User?.FindFirstValue("isAdmin") == "true";
        var isModerator = Context.User?.FindFirstValue("isModerator") == "true";

        if (!isAdmin && !isModerator)
        {
            await Clients.Caller.SendAsync("ChatError", "You don't have permission to delete messages.");
            return;
        }

        await chatService.DeleteMessageAsync(messageId);
        await Clients.All.SendAsync("MessageDeleted", messageId);
    }

    public async Task BroadcastSystemMessage(string message)
    {
        var chatMessage = await chatService.AddMessageAsync("", message, false, isSystem: true);
        await Clients.All.SendAsync("ReceiveMessage", chatMessage);
    }
}