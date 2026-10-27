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
        string mode = "classic",
        Guid? challengeOf = null,
        CancellationToken cancellationToken = default)
    {
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Mode = mode,
            ChallengeOf = challengeOf,
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

    public async Task<IReadOnlyList<GameSession>> GetTopSessionsAsync(int count = 10, DateTime? since = null, IReadOnlyCollection<string>? modes = null, CancellationToken cancellationToken = default)
        => await Completed(since, modes)
            .Include(g => g.Player)
            .Include(g => g.Scores)
                .ThenInclude(s => s.Song)
            .OrderByDescending(g => g.TotalPoints)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<GameSession?> GetBestSessionAsync(Guid playerId, DateTime? since = null, IReadOnlyCollection<string>? modes = null, CancellationToken cancellationToken = default)
        => await Completed(since, modes)
            .Include(g => g.Player)
            .Include(g => g.Scores)
                .ThenInclude(s => s.Song)
            .Where(g => g.PlayerId == playerId)
            .OrderByDescending(g => g.TotalPoints)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<GameSession> Completed(DateTime? since, IReadOnlyCollection<string>? modes = null)
    {
        var q = _context.GameSessions.Where(g => g.IsComplete);
        if (since is not null) q = q.Where(g => g.CompletedAt >= since);
        if (modes is not null) q = q.Where(g => modes.Contains(g.Mode));
        return q;
    }

    public Task<int> CountCompletedAsync(DateTime? since = null, IReadOnlyCollection<string>? modes = null, CancellationToken cancellationToken = default)
        => Completed(since, modes).CountAsync(cancellationToken);

    public async Task<int> GetSessionRankAsync(Guid sessionId, DateTime? since = null, IReadOnlyCollection<string>? modes = null, CancellationToken cancellationToken = default)
    {
        var session = await _context.GameSessions
            .FirstOrDefaultAsync(g => g.Id == sessionId, cancellationToken);

        if (session is null) return -1;

        return await Completed(since, modes)
            .Where(g => g.TotalPoints > session.TotalPoints)
            .CountAsync(cancellationToken) + 1;
    }
}
