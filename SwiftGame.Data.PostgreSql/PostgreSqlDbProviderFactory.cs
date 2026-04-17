using Microsoft.EntityFrameworkCore;
using SwiftGame.Data;

namespace SwiftGame.Data.PostgreSql;

public class PostgreSqlDbProviderFactory : IDbProviderFactory
{
    public string ProviderName => "PostgreSql";
    public string MigrationsAssembly => "SwiftGame.Data.PostgreSql";

    public void ConfigureDbContext(
        DbContextOptionsBuilder options,
        string connectionString)
        => options.UseNpgsql(
               connectionString,
               npgsql => npgsql.MigrationsAssembly(MigrationsAssembly));
}