using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public interface ISongRepository
{
    Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Song?> GetByProviderIdAsync(string providerId, string provider, CancellationToken cancellationToken = default);

    Task UpsertAsync(Song song, CancellationToken cancellationToken = default);

    Task UpsertManyAsync(IEnumerable<Song> songs, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Song>> GetRandomSongsAsync(int count, List<Guid>? excludeIds = null, CancellationToken cancellationToken = default, List<Guid>? albumIds = null);

    Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // One song per title from included albums, in a stable order (for seeded daily sets)
    Task<IReadOnlyList<Guid>> GetPlayablePoolAsync(CancellationToken cancellationToken = default);
}