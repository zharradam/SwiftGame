using Microsoft.EntityFrameworkCore;

namespace SwiftGame.Data;

public interface IDbProviderFactory
{
    string ProviderName { get; }
    string MigrationsAssembly { get; }

    void ConfigureDbContext(
        DbContextOptionsBuilder options,
        string connectionString);
}