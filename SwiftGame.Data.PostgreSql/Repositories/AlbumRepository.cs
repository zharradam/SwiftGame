using Microsoft.EntityFrameworkCore;
using SwiftGame.Data;
using SwiftGame.Data.Entities;
using SwiftGame.Data.Repositories;

namespace SwiftGame.Data.PostgreSql.Repositories;

public class AlbumRepository(SwiftGameDbContext db) : IAlbumRepository
{
    public async Task<IReadOnlyList<Album>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Albums
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

    public Task<Album?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Albums.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Album?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        db.Albums.FirstOrDefaultAsync(a => a.Name == name, cancellationToken);

    public async Task CreateAsync(Album album, CancellationToken cancellationToken = default)
    {
        db.Albums.Add(album);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Album album, CancellationToken cancellationToken = default)
    {
        db.Albums.Update(album);
        await db.SaveChangesAsync(cancellationToken);
    }

    // Populates Albums table from distinct album names in Songs
    // and links each Song to its Album via AlbumId
    public async Task SeedFromSongsAsync(CancellationToken cancellationToken = default)
    {
        var distinctAlbums = await db.Songs
            .Select(s => s.Album)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var albumName in distinctAlbums)
        {
            if (string.IsNullOrWhiteSpace(albumName)) continue;

            var existing = await db.Albums
                .FirstOrDefaultAsync(a => a.Name == albumName, cancellationToken);

            if (existing is null)
            {
                existing = new Album { Name = albumName, IsIncluded = true };
                db.Albums.Add(existing);
                await db.SaveChangesAsync(cancellationToken);
            }

            // Link all songs with this album name to the album record
            var songs = await db.Songs
                .Where(s => s.Album == albumName && s.AlbumId == null)
                .ToListAsync(cancellationToken);

            foreach (var song in songs)
                song.AlbumId = existing.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}