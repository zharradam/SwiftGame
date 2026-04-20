using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SwiftGame.API.Hubs;
using SwiftGame.API.Services;
using SwiftGame.API.Settings;
using SwiftGame.Data;
using SwiftGame.Data.PostgreSql;
using SwiftGame.Data.Repositories;
using SwiftGame.Data.SqlServer;
using SwiftGame.Music.Abstractions;
using SwiftGame.Music.Fallback;
using SwiftGame.Music.iTunes;
using SwiftGame.Music.Spotify;
using SwiftGame.Music.Spotify.Config;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers & API explorer ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IChatService, ChatService>();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.Configure<GameSettings>(
    builder.Configuration.GetSection("GameSettings"));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secret = jwtSettings["Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Manually extract Bearer token from Authorization header. Required due to a version compatibility issue between
            // Microsoft.AspNetCore.Authentication.JwtBearer and Microsoft.IdentityModel.Tokens where the header is not automatically parsed.
            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(context.Token) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = authHeader.Substring("Bearer ".Length).Trim();
            }

            // Allow SignalR hubs to receive the token from the query string
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("SwiftGameClient", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "https://zharradam.github.io",
                "https://swiftology.uk",
                "https://www.swiftology.uk"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
});

// ── Database provider factory ─────────────────────────────────────────────────
var dbProviderName = builder.Configuration["DatabaseProvider"] ?? "SqlServer";

IDbProviderFactory dbProviderFactory = dbProviderName switch
{
    "PostgreSql" => new PostgreSqlDbProviderFactory(),
    _ => new SqlServerDbProviderFactory()
};

builder.Services.AddSingleton<IDbProviderFactory>(dbProviderFactory);

builder.Services.AddDbContext<SwiftGameDbContext>(options =>
    dbProviderFactory.ConfigureDbContext(
        options,
        builder.Configuration.GetConnectionString("SwiftGameDb")!
    )
);

// ── Music provider factory ────────────────────────────────────────────────────
builder.Services.Configure<SpotifyConfig>(
    builder.Configuration.GetSection("Spotify"));

builder.Services.AddHttpClient<SpotifyMusicFactory>();
builder.Services.AddHttpClient<ItunesMusicFactory>();

builder.Services.AddSingleton<IMusicProviderFactory>(sp =>
    new FallbackMusicFactory(
        sp.GetRequiredService<ItunesMusicFactory>(),
        sp.GetRequiredService<SpotifyMusicFactory>()
    )
);

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ILeaderboardRepository, LeaderboardRepository>();
builder.Services.AddScoped<ISongRepository, SongRepository>();
builder.Services.AddScoped<IGameSessionRepository, GameSessionRepository>();
builder.Services.AddScoped<IPlayerRepository>(sp =>
{
    var db = sp.GetRequiredService<SwiftGameDbContext>();
    return dbProviderName == "PostgreSql"
        ? new SwiftGame.Data.PostgreSql.Repositories.PlayerRepository(db)
        : new SwiftGame.Data.SqlServer.Repositories.PlayerRepository(db);
});
builder.Services.AddScoped<IAlbumRepository>(sp =>
{
    var db = sp.GetRequiredService<SwiftGameDbContext>();
    return dbProviderName == "PostgreSql"
        ? new SwiftGame.Data.PostgreSql.Repositories.AlbumRepository(db)
        : new SwiftGame.Data.SqlServer.Repositories.AlbumRepository(db);
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("SwiftGameClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<LeaderboardHub>("/hubs/leaderboard");
app.MapHub<ChatHub>("/hubs/chat");

// ── Auto-apply migrations on startup ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SwiftGameDbContext>();
    await db.Database.MigrateAsync();

    // Seed guest player for unauthenticated games
    var guestExists = await db.Players.AnyAsync(p => p.Id == Guid.Empty);
    if (!guestExists)
    {
        db.Players.Add(new SwiftGame.Data.Entities.Player
        {
            Id = Guid.Empty,
            Username = "Guest",
            Email = "guest@swiftgame.com",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // Seed albums from songs — only runs if Albums table is empty
    if (!await db.Albums.AnyAsync())
    {
        var albumRepo = scope.ServiceProvider.GetRequiredService<IAlbumRepository>();
        await albumRepo.SeedFromSongsAsync();
    }
}

await app.RunAsync();