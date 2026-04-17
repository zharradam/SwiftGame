using Microsoft.AspNetCore.SignalR;

namespace SwiftGame.API.Hubs;

public class LeaderboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "leaderboard");
        await base.OnConnectedAsync();
    }
}