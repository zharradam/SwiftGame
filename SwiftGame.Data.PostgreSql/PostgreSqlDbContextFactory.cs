using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SwiftGame.Data;

namespace SwiftGame.Data.PostgreSql;

public class PostgreSqlDbContextFactory : IDesignTimeDbContextFactory<SwiftGameDbContext>
{
    public SwiftGameDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "SwiftGame.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Production.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SwiftGameDbContext>();
        new PostgreSqlDbProviderFactory().ConfigureDbContext(
            optionsBuilder,
            configuration.GetConnectionString("SwiftGameDb")!);

        return new SwiftGameDbContext(optionsBuilder.Options);
    }
}