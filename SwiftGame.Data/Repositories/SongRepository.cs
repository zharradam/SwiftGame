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

    public async Task<IReadOnlyList<Song>> GetRandomSongsAsync(
        int count,
        List<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default,
        List<Guid>? albumIds = null)
    {
        // Only pick from songs belonging to included albums
        // Songs with no AlbumId (not yet linked) are included as a fallback
        var query = _context.Songs
            .Where(s => s.PreviewUrl != null)
            .Where(s => excludeIds == null || !excludeIds.Contains(s.Id))
            .Where(s => s.AlbumId == null ||                          // unlinked songs — include as fallback
                        s.AlbumRef!.IsIncluded);                      // or album is included

        // Album mode: the player's chosen albums, whether or not the admin includes them by default
        if (albumIds is { Count: > 0 })
            query = _context.Songs
                .Where(s => s.PreviewUrl != null && s.AlbumId != null && albumIds.Contains(s.AlbumId.Value))
                .Where(s => excludeIds == null || !excludeIds.Contains(s.Id));

        var allIds = await query
            .Select(s => new { s.Id, s.Title })
            .ToListAsync(cancellationToken);

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

    public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Songs.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetPlayablePoolAsync(CancellationToken cancellationToken = default)
    {
        var all = await _context.Songs
            .Where(s => s.PreviewUrl != null)
            .Where(s => s.AlbumId == null || s.AlbumRef!.IsIncluded)
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Title })
            .ToListAsync(cancellationToken);

        return all.GroupBy(s => NormaliseTitle(s.Title)).Select(g => g.First().Id).ToList();
    }

    // Strip "(Taylor's Version)", "[From The Vault]" etc. so versions of one song count once
    public static string NormaliseTitle(string title) => System.Text.RegularExpressions.Regex
        .Replace(title, @"\s*[\(\[].*?[\)\]]", "")
        .Trim()
        .ToLowerInvariant();
}
