using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SwiftGame.Data;

namespace SwiftGame.Data.SqlServer;

public class SqlServerDbContextFactory : IDesignTimeDbContextFactory<SwiftGameDbContext>
{
    public SwiftGameDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "SwiftGame.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SwiftGameDbContext>();
        new SqlServerDbProviderFactory().ConfigureDbContext(
            optionsBuilder,
            configuration.GetConnectionString("SwiftGameDb")!);

        return new SwiftGameDbContext(optionsBuilder.Options);
    }
}