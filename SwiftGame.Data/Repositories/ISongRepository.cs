using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public interface ISongRepository
{
    Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Song?> GetByProviderIdAsync(string providerId, string provider, CancellationToken cancellationToken = default);

    Task UpsertAsync(Song song, CancellationToken cancellationToken = default);

    Task UpsertManyAsync(IEnumerable<Song> songs, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Song>> GetRandomSongsAsync(int count, CancellationToken cancellationToken = default);
}