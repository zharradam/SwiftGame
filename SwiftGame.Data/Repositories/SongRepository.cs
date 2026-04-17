using Microsoft.EntityFrameworkCore;
using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public class SongRepository : ISongRepository
{
    private readonly SwiftGameDbContext _context;

    public SongRepository(SwiftGameDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Songs
            .OrderBy(s => s.Title)
            .ToListAsync(cancellationToken);

    public async Task<Song?> GetByProviderIdAsync(string providerId, string provider, CancellationToken cancellationToken = default)
        => await _context.Songs
            .FirstOrDefaultAsync(
                s => s.ProviderId == providerId && s.Provider == provider,
                cancellationToken);

    public async Task UpsertAsync(Song song, CancellationToken cancellationToken = default)
    {
        var existing = await GetByProviderIdAsync(
            song.ProviderId, song.Provider, cancellationToken);

        if (existing is null)
        {
            song.Id = Guid.NewGuid();
            await _context.Songs.AddAsync(song, cancellationToken);
        }
        else
        {
            existing.Title = song.Title;
            existing.Album = song.Album;
            existing.AlbumArt = song.AlbumArt;
            existing.PreviewUrl = song.PreviewUrl;
            existing.DurationMs = song.DurationMs;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertManyAsync(IEnumerable<Song> songs, CancellationToken cancellationToken = default)
    {
        foreach (var song in songs)
            await UpsertAsync(song, cancellationToken);
    }

    public async Task<IReadOnlyList<Song>> GetRandomSongsAsync(int count, CancellationToken cancellationToken = default)
    {
        var allIds = await _context.Songs
            .Where(s => s.PreviewUrl != null)
            .Select(s => new { s.Id, s.Title })
            .ToListAsync(cancellationToken);

        // Normalise title by stripping common version suffixes
        static string NormaliseTitle(string title) => System.Text.RegularExpressions.Regex
            .Replace(title, @"\s*[\(\[].*?[\)\]]", "")
            .Trim()
            .ToLowerInvariant();

        // Shuffle first then pick one song per normalised title
        var unique = allIds
            .OrderBy(_ => Random.Shared.Next())
            .GroupBy(s => NormaliseTitle(s.Title))
            .Select(g => g.First().Id)
            .Take(count)
            .ToList();

        return await _context.Songs
            .Where(s => unique.Contains(s.Id))
            .ToListAsync(cancellationToken);
    }
}