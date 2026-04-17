using SwiftGame.Music.Abstractions.Models;

namespace SwiftGame.Music.Abstractions;

public interface ITrackSearcher
{
    Task<IReadOnlyList<TrackResult>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<TrackResult?> GetByIdAsync(
        string providerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackResult>> GetByIdsAsync(
        IEnumerable<string> providerIds,
        CancellationToken cancellationToken = default);
}