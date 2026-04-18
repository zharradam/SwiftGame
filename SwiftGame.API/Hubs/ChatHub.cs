using Microsoft.AspNetCore.SignalR;
using SwiftGame.API.Services;
using System.Security.Claims;

namespace SwiftGame.API.Hubs;

public class ChatHub(IChatService chatService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var history = chatService.GetRecentMessages();
        await Clients.Caller.SendAsync("ChatHistory", history);
        await base.OnConnectedAsync();
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

        var chatMessage = chatService.AddMessage(display, filtered, isGuest);
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

        chatService.DeleteMessage(messageId);
        await Clients.All.SendAsync("MessageDeleted", messageId);
    }

    public async Task BroadcastSystemMessage(string message)
    {
        var chatMessage = chatService.AddMessage("", message, false, isSystem: true);
        await Clients.All.SendAsync("ReceiveMessage", chatMessage);
    }
}