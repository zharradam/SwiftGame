using SwiftGame.Data.Entities;

namespace SwiftGame.Data.Repositories;

public interface IPlayerRepository
{
    Task<bool> EmailExistsAsync(string email);
    Task<bool> UsernameExistsAsync(string username);

    Task<Player?> GetByEmailAsync(string email);
    Task<Player?> GetByIdAsync(Guid id);
    Task<Player?> GetByRefreshTokenAsync(string refreshToken);

    Task CreateAsync(Player player);
    Task UpdateAsync(Player player);
}