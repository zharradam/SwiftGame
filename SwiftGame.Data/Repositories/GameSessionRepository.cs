using Microsoft.EntityFrameworkCore;
using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public class GameSessionRepository : IGameSessionRepository
{
    private readonly SwiftGameDbContext _context;

    public GameSessionRepository(SwiftGameDbContext context)
        => _context = context;

    public async Task<GameSession> CreateAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            StartedAt = DateTime.UtcNow,
            IsComplete = false
        };

        await _context.GameSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<GameSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => await _context.GameSessions
            .Include(g => g.Scores)
            .FirstOrDefaultAsync(g => g.Id == sessionId, cancellationToken);

    public async Task CompleteAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.GameSessions
            .Include(g => g.Scores)
            .Include(g => g.Player)
            .FirstOrDefaultAsync(g => g.Id == sessionId, cancellationToken);

        if (session is null) return;

        session.IsComplete = true;
        session.CompletedAt = DateTime.UtcNow;
        session.TotalPoints = session.Scores.Sum(s => s.PointsEarned);
        session.QuestionsCount = session.Scores.Count;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameSession>> GetTopSessionsAsync(int count = 10, DateTime? since = null, CancellationToken cancellationToken = default)
        => await Completed(since)
            .Include(g => g.Player)
            .Include(g => g.Scores)
                .ThenInclude(s => s.Song)
            .OrderByDescending(g => g.TotalPoints)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<GameSession?> GetBestSessionAsync(Guid playerId, DateTime? since = null, CancellationToken cancellationToken = default)
        => await Completed(since)
            .Include(g => g.Player)
            .Include(g => g.Scores)
                .ThenInclude(s => s.Song)
            .Where(g => g.PlayerId == playerId)
            .OrderByDescending(g => g.TotalPoints)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<GameSession> Completed(DateTime? since)
        => since is null
            ? _context.GameSessions.Where(g => g.IsComplete)
            : _context.GameSessions.Where(g => g.IsComplete && g.CompletedAt >= since);

    public Task<int> CountCompletedAsync(CancellationToken cancellationToken = default)
        => _context.GameSessions.CountAsync(g => g.IsComplete, cancellationToken);

    public async Task<int> GetSessionRankAsync(Guid sessionId, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var session = await _context.GameSessions
            .FirstOrDefaultAsync(g => g.Id == sessionId, cancellationToken);

        if (session is null) return -1;

        return await Completed(since)
            .Where(g => g.TotalPoints > session.TotalPoints)
            .CountAsync(cancellationToken) + 1;
    }
}
