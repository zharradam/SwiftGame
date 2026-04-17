using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public interface IGameSessionRepository
{
    Task<GameSession> CreateAsync(Guid playerId, CancellationToken cancellationToken = default);

    Task<GameSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameSession>> GetTopSessionsAsync(int count = 10, CancellationToken cancellationToken = default);

    Task<int> GetSessionRankAsync(Guid sessionId, CancellationToken cancellationToken = default);
}