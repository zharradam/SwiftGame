using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SwiftGame.API.Hubs;
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

var builder = WebApplication.CreateBuilder(args);

// ── Controllers & API explorer ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.Configure<GameSettings>(
    builder.Configuration.GetSection("GameSettings"));

// ── CORS (for Angular dev server on localhost:4200) ───────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("SwiftGameClient", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "https://swiftgame.azurewebsites.net"
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

var connString = builder.Configuration.GetConnectionString("SwiftGameDb");
var providerName = builder.Configuration["DatabaseProvider"];
Console.WriteLine($"=== DB DEBUG ===");
Console.WriteLine($"Provider: {providerName}");
Console.WriteLine($"Connection: {connString}");
Console.WriteLine($"================");

builder.Services.AddDbContext<SwiftGameDbContext>(options =>
    dbProviderFactory.ConfigureDbContext(
        options,
        builder.Configuration.GetConnectionString("SwiftGameDb")!
    )
);

// ── Redis cache (leaderboard) ─────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "SwiftGame:";
});

// ── Music provider factory ────────────────────────────────────────────────────
builder.Services.Configure<SpotifyConfig>(
    builder.Configuration.GetSection("Spotify"));

// Register both concrete factories
builder.Services.AddHttpClient<SpotifyMusicFactory>();
builder.Services.AddHttpClient<ItunesMusicFactory>();

// The fallback factory is what the rest of the app sees —
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

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("SwiftGameClient");
app.UseAuthorization();
app.MapControllers();
app.MapHub<LeaderboardHub>("/hubs/leaderboard");

// ── Auto-apply migrations on startup ─────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SwiftGameDbContext>();
    await db.Database.MigrateAsync();

    // ── Seed guest player (placeholder until auth is implemented) ─────────────────
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
}

await app.RunAsync();