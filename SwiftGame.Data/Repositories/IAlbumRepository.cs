using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public interface IAlbumRepository
{
    Task<IReadOnlyList<Album>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Album?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Album?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task CreateAsync(Album album, CancellationToken cancellationToken = default);
    Task UpdateAsync(Album album, CancellationToken cancellationToken = default);
    Task SeedFromSongsAsync(CancellationToken cancellationToken = default);
}