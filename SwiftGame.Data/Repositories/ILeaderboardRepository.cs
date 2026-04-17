using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public interface ILeaderboardRepository
{
    Task<IReadOnlyList<Score>> GetTopScoresAsync(
        int count = 10,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Score>> GetPlayerScoresAsync(
        Guid playerId,
        int count = 10,
        CancellationToken cancellationToken = default);

    Task AddScoreAsync(
        Score score,
        CancellationToken cancellationToken = default);
}