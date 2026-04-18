using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwiftGame.API.Models.Auth;
using SwiftGame.API.Services;
using SwiftGame.Data;
using SwiftGame.Data.Entities;
using SwiftGame.Data.Repositories;
using System.Security.Claims;

namespace SwiftGame.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IPlayerRepository repo,
    IJwtService jwt,
    IConfiguration config,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly int _refreshExpiryDays =
        config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);

    // ── POST /api/auth/register ───────────────────────────────────────────────

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length < 2 || req.Username.Length > 15)
            return BadRequest(new { message = "Username must be 2–15 characters." });

        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return BadRequest(new { message = "Invalid email address." });

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest(new { message = "Password must be at least 6 characters." });

        // Duplicate checks
        if (await repo.EmailExistsAsync(req.Email))
            return Conflict(new { message = "An account with that email already exists." });

        if (await repo.UsernameExistsAsync(req.Username))
            return Conflict(new { message = "That username is already taken." });

        var player = new Player
        {
            Username = req.Username.Trim(),
            Email = req.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            RefreshToken = jwt.GenerateRefreshToken(),
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshExpiryDays)
        };

        await repo.CreateAsync(player);
        logger.LogInformation("New player registered: {Username} ({Email})", player.Username, player.Email);

        return Ok(BuildAuthResponse(player));
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var player = await repo.GetByEmailAsync(req.Email.Trim().ToLowerInvariant());

        if (player is null || player.PasswordHash is null ||
            !BCrypt.Net.BCrypt.Verify(req.Password, player.PasswordHash))
        {
            // Generic message — don't reveal which field is wrong
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // Rotate refresh token on every login
        player.RefreshToken = jwt.GenerateRefreshToken();
        player.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshExpiryDays);
        await repo.UpdateAsync(player);

        return Ok(BuildAuthResponse(player));
    }

    // ── POST /api/auth/refresh ────────────────────────────────────────────────

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest req)
    {
        // We accept expired access tokens here — just need the player ID from claims
        var principal = jwt.ValidateAccessToken(req.RefreshToken);

        // The refresh token itself is opaque — look it up directly in the DB
        var player = await repo.GetByRefreshTokenAsync(req.RefreshToken);

        if (player is null || player.RefreshTokenExpiry < DateTime.UtcNow)
            return Unauthorized(new { message = "Refresh token is invalid or expired." });

        // Rotate refresh token
        player.RefreshToken = jwt.GenerateRefreshToken();
        player.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshExpiryDays);
        await repo.UpdateAsync(player);

        return Ok(BuildAuthResponse(player));
    }

    // ── POST /api/auth/logout ─────────────────────────────────────────────────

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var playerId = GetPlayerIdFromToken();
        if (playerId is null) return Unauthorized();

        var player = await repo.GetByIdAsync(playerId.Value);
        if (player is not null)
        {
            player.RefreshToken = null;
            player.RefreshTokenExpiry = null;
            await repo.UpdateAsync(player);
        }

        return NoContent();
    }

    // ── GET /api/auth/me ──────────────────────────────────────────────────────

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var playerId = GetPlayerIdFromToken();
        if (playerId is null) return Unauthorized();

        var player = await repo.GetByIdAsync(playerId.Value);
        if (player is null) return NotFound();

        return Ok(new UserDto(player.Id, player.Username, player.Email));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private AuthResponse BuildAuthResponse(Player player)
    {
        var accessToken = jwt.GenerateAccessToken(player);
        var expiry = DateTime.UtcNow.AddMinutes(
            config.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 60));

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: player.RefreshToken!,
            AccessTokenExpiry: expiry,
            User: new UserDto(player.Id, player.Username, player.Email)
        );
    }

    private Guid? GetPlayerIdFromToken()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}