using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.Core.Contracts.Infrastructure;
using Nocturne.Infrastructure.Data.Entities.V4;
using Nocturne.Infrastructure.Data.Services;

namespace Nocturne.Infrastructure.Data.Tests.Services;

/// <summary>
/// Tests for the reusable per-record criteria + deleted-status loader used by
/// dedup reconciliation. Verifies that <see cref="DeduplicationService"/> can
/// load each record's <see cref="MatchCriteria"/> alongside its soft-deleted
/// status, keyed by record id, even for soft-deleted rows.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Deduplication")]
public class DeduplicationReconcileTests : IDisposable
{
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly DbConnection _connection;
    private readonly DbContextOptions<NocturneDbContext> _contextOptions;
    private readonly NocturneDbContext _context;
    private readonly DeduplicationService _service;

    public DeduplicationReconcileTests()
    {
        // In-memory SQLite database for testing — mirrors CarbIntakeRepositoryTests.
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        // Create the database schema and seed the tenant.
        using (var seedContext = new NocturneDbContext(_contextOptions))
        {
            seedContext.TenantId = TestTenantId;
            seedContext.Database.EnsureCreated();
            seedContext.Tenants.Add(new TenantEntity { Id = TestTenantId, Slug = "test" });
            seedContext.SaveChanges();
        }

        _context = new NocturneDbContext(_contextOptions);
        _context.TenantId = TestTenantId;

        _service = new DeduplicationService(
            _context,
            new Mock<IServiceScopeFactory>().Object,
            NullLogger<DeduplicationService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task LoadRecordInfoAsync_CarbIntake_ReturnsCriteriaAndDeletedFlag()
    {
        var live = Guid.CreateVersion7();
        var gone = Guid.CreateVersion7();
        _context.CarbIntakes.Add(new CarbIntakeEntity { Id = live, TenantId = TestTenantId, Carbs = 42, Timestamp = DateTime.UtcNow });
        _context.CarbIntakes.Add(new CarbIntakeEntity { Id = gone, TenantId = TestTenantId, Carbs = 42, Timestamp = DateTime.UtcNow, DeletedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var info = await _service.LoadRecordInfoForTestAsync(RecordType.CarbIntake, new HashSet<Guid> { live, gone });

        info[live].Criteria.Carbs.Should().Be(42);
        info[live].IsDeleted.Should().BeFalse();
        info[gone].IsDeleted.Should().BeTrue();
    }
}
