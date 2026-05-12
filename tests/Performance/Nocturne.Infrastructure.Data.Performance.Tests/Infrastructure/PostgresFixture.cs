using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace Nocturne.Infrastructure.Data.Performance.Tests.Infrastructure;

public class PostgresFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;
    private string _connectionString = null!;

    public PostgresFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:17.6")
            .WithDatabase("nocturne_perf")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public NocturneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new NocturneDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
