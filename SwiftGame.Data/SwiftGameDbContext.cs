using Microsoft.EntityFrameworkCore;
using SwiftGame.Data.Entities;

namespace SwiftGame.Data;

public class SwiftGameDbContext : DbContext
{
    public SwiftGameDbContext(DbContextOptions<SwiftGameDbContext> options)
        : base(options)
    {
    }

    public DbSet<Player> Players { get; set; }
    public DbSet<Score> Scores { get; set; }
    public DbSet<Song> Songs { get; set; }
    public DbSet<GameSession> GameSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Player ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id)
      .ValueGeneratedNever();

            entity.Property(p => p.Username)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(p => p.Email)
                  .IsRequired()
                  .HasMaxLength(255);

            entity.HasIndex(p => p.Email)
                  .IsUnique();

            entity.HasIndex(p => p.Username)
                  .IsUnique();
        });

        // ── Song ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<Song>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.ProviderId)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(s => s.Provider)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(s => s.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(s => s.Album)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(s => s.AlbumArt)
                  .HasMaxLength(500);

            entity.Property(s => s.ArtistName)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(s => s.PreviewUrl)
                  .HasMaxLength(500);

            // Prevent duplicate songs from the same provider
            entity.HasIndex(s => new { s.ProviderId, s.Provider })
                  .IsUnique();
        });

        // ── GameSession ───────────────────────────────────────────────────────────────
        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.HasKey(g => g.Id);

            entity.Property(g => g.Id)
                  .ValueGeneratedNever();

            entity.HasOne(g => g.Player)
                  .WithMany(p => p.GameSessions)
                  .HasForeignKey(g => g.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Update Score to include GameSession ───────────────────────────────────────
        modelBuilder.Entity<Score>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.HasOne(s => s.Player)
                  .WithMany(p => p.Scores)
                  .HasForeignKey(s => s.PlayerId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(s => s.Song)
                  .WithMany(so => so.Scores)
                  .HasForeignKey(s => s.SongId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.GameSession)
                  .WithMany(g => g.Scores)
                  .HasForeignKey(s => s.GameSessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}