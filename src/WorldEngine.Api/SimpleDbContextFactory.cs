using Microsoft.EntityFrameworkCore;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Api;

public sealed class SimpleDbContextFactory : IDbContextFactory<WorldEngineDbContext>
{
    private readonly DbContextOptions<WorldEngineDbContext> _options;

    public SimpleDbContextFactory(DbContextOptions<WorldEngineDbContext> options)
    {
        _options = options;
    }

    public WorldEngineDbContext CreateDbContext() => new(_options);
}