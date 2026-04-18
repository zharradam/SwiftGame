using Microsoft.AspNetCore.SignalR;
using SwiftGame.API.Services;
using System.Security.Claims;

namespace SwiftGame.API.Hubs;

public class ChatHub(IChatService chatService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Send message history to the newly connected client
        var history = chatService.GetRecentMessages();
        await Clients.Caller.SendAsync("ChatHistory", history);
        await base.OnConnectedAsync();
    }

    public async Task SendMessage(string message)
    {
        var username = Context.User?.FindFirstValue("username")
                    ?? Context.User?.FindFirstValue(ClaimTypes.Name);

        var isGuest = username is null;
        var display = username ?? "Guest";

        // Rate limiting
        if (chatService.IsRateLimited(display))
        {
            await Clients.Caller.SendAsync("ChatError", "You're sending messages too fast.");
            return;
        }

        var (allowed, filtered) = chatService.ProcessMessage(display, message, isGuest);

        if (!allowed)
        {
            await Clients.Caller.SendAsync("ChatError", "Guests cannot send messages. Please log in.");
            return;
        }

        var chatMessage = chatService.AddMessage(display, filtered, isGuest);

        // Broadcast to all connected clients
        await Clients.All.SendAsync("ReceiveMessage", chatMessage);
    }

    public async Task BroadcastSystemMessage(string message)
    {
        var chatMessage = chatService.AddMessage("", message, false, isSystem: true);
        await Clients.All.SendAsync("ReceiveMessage", chatMessage);
    }
}