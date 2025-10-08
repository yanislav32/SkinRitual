// Data/BotDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SkinRitual.Data;

internal sealed class SecretsMarker { }

public class BotDbContextFactory : IDesignTimeDbContextFactory<BotDbContext>
{
    public BotDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets<SecretsMarker>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = cfg.GetConnectionString("BotDb")
            ?? throw new InvalidOperationException("ConnectionStrings:BotDb missing.");

        // отладка: проверяем кем/куда пойдём
        var csb = new NpgsqlConnectionStringBuilder(conn);
        Console.WriteLine($"[EF] Host={csb.Host};Db={csb.Database};User={csb.Username};Port={csb.Port};SearchPath={csb.SearchPath}");

        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseNpgsql(conn, b =>
            {
                // ВАЖНО: таблица истории в схеме public
                b.MigrationsHistoryTable("__EFMigrationsHistory", "sr");
            })
            .Options;

        return new BotDbContext(options);
    }
}
