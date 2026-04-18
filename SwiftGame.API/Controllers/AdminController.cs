using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SwiftGame.API.Hubs;
using SwiftGame.API.Services;
using SwiftGame.Data.Repositories;

namespace SwiftGame.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController(
    IPlayerRepository players,
    IChatService chatService,
    IHubContext<ChatHub> chatHub) : ControllerBase
{
    private bool IsAdmin => User.FindFirst("isAdmin")?.Value == "true";
    private bool CanModerate => User.FindFirst("isAdmin")?.Value == "true"
                              || User.FindFirst("isModerator")?.Value == "true";

    // ── GET /api/admin/players ────────────────────────────────────────────────

    [HttpGet("players")]
    public async Task<IActionResult> GetPlayers()
    {
        if (!IsAdmin) return Forbid();

        var playerList = await players.GetAllPlayersAsync();

        var result = playerList.Select(p => new
        {
            p.Id,
            p.Username,
            p.Email,
            p.IsAdmin,
            p.IsModerator,
            p.IsBanned,
            p.CreatedAt
        });

        return Ok(result);
    }

    // ── POST /api/admin/players/{id}/ban ──────────────────────────────────────

    [HttpPost("players/{id}/ban")]
    public async Task<IActionResult> BanPlayer(Guid id)
    {
        if (!IsAdmin) return Forbid();

        var player = await players.GetByIdAsync(id);
        if (player is null) return NotFound();
        if (player.IsAdmin) return BadRequest(new { message = "Cannot ban an admin." });

        player.IsBanned = true;
        await players.UpdateAsync(player);

        // Remove all their messages from chat
        chatService.DeleteMessagesByUser(player.Username);

        // Broadcast to all clients
        await chatHub.Clients.All.SendAsync("PlayerBanned", player.Username);
        await chatHub.Clients.All.SendAsync("ChatHistory", chatService.GetRecentMessages());

        return Ok(new { message = $"{player.Username} has been banned." });
    }

    // ── POST /api/admin/players/{id}/unban ────────────────────────────────────

    [HttpPost("players/{id}/unban")]
    public async Task<IActionResult> UnbanPlayer(Guid id)
    {
        if (!IsAdmin) return Forbid();

        var player = await players.GetByIdAsync(id);
        if (player is null) return NotFound();

        player.IsBanned = false;
        await players.UpdateAsync(player);

        await chatHub.Clients.All.SendAsync("PlayerUnbanned", player.Username);

        return Ok(new { message = $"{player.Username} has been unbanned." });
    }

    // ── POST /api/admin/players/{id}/moderator ────────────────────────────────

    [HttpPost("players/{id}/moderator")]
    public async Task<IActionResult> ToggleModerator(Guid id)
    {
        if (!IsAdmin) return Forbid();

        var player = await players.GetByIdAsync(id);
        if (player is null) return NotFound();
        if (player.IsAdmin) return BadRequest(new { message = "Cannot modify admin role." });

        player.IsModerator = !player.IsModerator;
        await players.UpdateAsync(player);

        return Ok(new { message = $"{player.Username} moderator status: {player.IsModerator}" });
    }

    // ── DELETE /api/admin/messages/{id} ──────────────────────────────────────

    [HttpDelete("messages/{messageId}")]
    public async Task<IActionResult> DeleteMessage(string messageId)
    {
        if (!CanModerate) return Forbid();

        chatService.DeleteMessage(messageId);
        await chatHub.Clients.All.SendAsync("MessageDeleted", messageId);

        return NoContent();
    }
}