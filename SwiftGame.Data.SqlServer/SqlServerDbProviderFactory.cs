using Microsoft.EntityFrameworkCore;
using SwiftGame.Data;

namespace SwiftGame.Data.SqlServer;

public class SqlServerDbProviderFactory : IDbProviderFactory
{
    public string ProviderName => "SqlServer";
    public string MigrationsAssembly => "SwiftGame.Data.SqlServer";

    public void ConfigureDbContext(
        DbContextOptionsBuilder options,
        string connectionString)
        => options.UseSqlServer(
               connectionString,
               sql => sql.MigrationsAssembly(MigrationsAssembly));
}