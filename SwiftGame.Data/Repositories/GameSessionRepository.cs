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
            .FirstOrDefaultAsync(g => g.Id == sessionId, cancellationToken);

        if (session is null) return;

        session.IsComplete = true;
        session.CompletedAt = DateTime.UtcNow;
        session.TotalPoints = session.Scores.Sum(s => s.PointsEarned);
        session.QuestionsCount = session.Scores.Count;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameSession>> GetTopSessionsAsync(int count = 10, CancellationToken cancellationToken = default)
        => await _context.GameSessions
            .Include(g => g.Player)
            .Include(g => g.Scores)
                .ThenInclude(s => s.Song)
            .Where(g => g.IsComplete)
            .OrderByDescending(g => g.TotalPoints)
            .Take(count == int.MaxValue ? int.MaxValue : count)
            .ToListAsync(cancellationToken);

    public async Task<int> GetSessionRankAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.GameSessions
            .FirstOrDefaultAsync(g => g.Id == sessionId, cancellationToken);

        if (session is null) return -1;

        var rank = await _context.GameSessions
            .Where(g => g.IsComplete && g.TotalPoints > session.TotalPoints)
            .CountAsync(cancellationToken) + 1;

        return rank;
    }
}