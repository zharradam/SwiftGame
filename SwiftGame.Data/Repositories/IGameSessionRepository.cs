using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public interface IGameSessionRepository
{
    Task<GameSession> CreateAsync(Guid playerId, string mode = "classic", Guid? challengeOf = null, CancellationToken cancellationToken = default);

    Task<GameSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameSession>> GetTopSessionsAsync(int count = 10, DateTime? since = null, IReadOnlyCollection<string>? modes = null, CancellationToken cancellationToken = default);

    Task<GameSession?> GetBestSessionAsync(Guid playerId, DateTime? since = null, IReadOnlyCollection<string>? modes = null, CancellationToken cancellationToken = default);

    Task<int> CountCompletedAsync(DateTime? since = null, IReadOnlyCollection<string>? modes = null, CancellationToken cancellationToken = default);

    Task<int> GetSessionRankAsync(Guid sessionId, DateTime? since = null, IReadOnlyCollection<string>? modes = null, CancellationToken cancellationToken = default);
}