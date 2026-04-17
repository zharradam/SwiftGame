using Microsoft.EntityFrameworkCore;
using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public class LeaderboardRepository : ILeaderboardRepository
{
    private readonly SwiftGameDbContext _context;

    public LeaderboardRepository(SwiftGameDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<Score>> GetTopScoresAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
        => await _context.Scores
            .Include(s => s.Player)
            .Include(s => s.Song)
            .Where(s => s.IsCorrect)
            .OrderByDescending(s => s.PointsEarned)
            .ThenBy(s => s.ResponseTimeMs)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Score>> GetPlayerScoresAsync(
        Guid playerId,
        int count = 10,
        CancellationToken cancellationToken = default)
        => await _context.Scores
            .Include(s => s.Song)
            .Where(s => s.PlayerId == playerId && s.IsCorrect)
            .OrderByDescending(s => s.PointsEarned)
            .ThenBy(s => s.ResponseTimeMs)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task AddScoreAsync(
        Score score,
        CancellationToken cancellationToken = default)
    {
        score.Id = Guid.NewGuid();
        await _context.Scores.AddAsync(score, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}