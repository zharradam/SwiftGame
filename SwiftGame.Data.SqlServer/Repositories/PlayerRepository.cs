using Microsoft.EntityFrameworkCore;
using SwiftGame.Data.Entities;
using SwiftGame.Data.Repositories;

namespace SwiftGame.Data.SqlServer.Repositories;

public class PlayerRepository(SwiftGameDbContext db) : IPlayerRepository
{
    public Task<bool> EmailExistsAsync(string email) =>
        db.Players.AnyAsync(p => p.Email == email);

    public Task<bool> UsernameExistsAsync(string username) =>
        db.Players.AnyAsync(p => p.Username == username);

    public Task<Player?> GetByEmailAsync(string email) =>
        db.Players.FirstOrDefaultAsync(p => p.Email == email);

    public Task<Player?> GetByIdAsync(Guid id) =>
        db.Players.FindAsync(id).AsTask();

    public Task<Player?> GetByRefreshTokenAsync(string refreshToken) =>
        db.Players.FirstOrDefaultAsync(p => p.RefreshToken == refreshToken);

    public async Task CreateAsync(Player player)
    {
        db.Players.Add(player);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Player player)
    {
        db.Players.Update(player);
        await db.SaveChangesAsync();
    }
}