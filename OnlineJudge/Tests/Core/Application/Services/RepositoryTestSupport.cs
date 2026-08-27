using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

// Shared setup for repository tests that exercise a real DbContext (EF
// InMemory provider) instead of mocking IXxxRepository. Bugs in EF's
// value-generation / relationship-fixup / insert-ordering only show up when
// SaveChanges actually runs, so these tests build the context and the
// AutoMapper configuration the same way the app's DI container does
// (see OnlineJudgeAdmin.Infrastructure.Database/DependencyInjection/ServiceCollectionExtensions.cs),
// scanning the whole assembly so every *Profile class is registered, not
// just one.
internal static class RepositoryTestSupport
{
    public static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static AcademicCatalogDbContext CreateAcademicContext() =>
        new(new DbContextOptionsBuilder<AcademicCatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static IMapper CreateMapper() =>
        new ServiceCollection()
            .AddLogging()
            .AddAutoMapper(_ => { }, typeof(AppDbContext).Assembly)
            .BuildServiceProvider()
            .GetRequiredService<IMapper>();

    // A handful of repository methods use ExecuteDeleteAsync/ExecuteUpdateAsync
    // or real transactions (BeginTransactionAsync), none of which the
    // InMemory provider supports (it throws). Sqlite's in-memory mode does
    // support both, so those specific tests use this instead. Pass a
    // context subclass that calls FixModelForSqlite from its
    // OnModelCreating (see SqliteAppDbContext / SqliteScheduleManagementDbContext).
    public static SqliteContext<TContext> CreateSqliteContext<TContext>(Func<DbContextOptions<TContext>, TContext> factory)
        where TContext : DbContext
        => new(factory);

    // Patches MySQL-only things out of the model so Sqlite can create the
    // schema, without touching the production model. Walks the whole model
    // instead of naming entities/properties one by one so this keeps
    // working as more repositories/entities get Sqlite-backed tests:
    //  - Column types like "int(10) unsigned" or "decimal(2,2) unsigned"
    //    are raw MySQL syntax; Sqlite's CREATE TABLE grammar doesn't parse
    //    a trailing word after the "(...)" part, so it fails with a syntax
    //    error at the "(". Clearing the override lets Sqlite's own default
    //    CLR-type mapping take over, which for a plain `int`/`int?`
    //    property is exactly "INTEGER" anyway — the one type name Sqlite
    //    requires, verbatim, for a store-generated PK to become the
    //    rowid-alias/AUTOINCREMENT column.
    //  - Collations like "utf8mb3_general_ci" (set per-entity, or as the
    //    model-wide default via modelBuilder.UseCollation) aren't known to
    //    Sqlite, which fails DDL generation with "no such collation
    //    sequence".
    //  - Raw default-value SQL like "current_timestamp()" is MySQL function
    //    syntax; in Sqlite, CURRENT_TIMESTAMP is a reserved literal token,
    //    not a callable identifier, so "(" right after it is also a syntax
    //    error. Tests always set these columns explicitly, so the default
    //    doesn't need to survive.
    public static void FixModelForSqlite(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation(null);
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                property.SetCollation(null);
                property.SetColumnType(null);
                property.SetDefaultValueSql(null);
            }
        }
    }
}

internal sealed class SqliteContext<TContext> : IDisposable where TContext : DbContext
{
    private readonly SqliteConnection _connection;

    public TContext Context { get; }

    public SqliteContext(Func<DbContextOptions<TContext>, TContext> factory)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Context = factory(new DbContextOptionsBuilder<TContext>().UseSqlite(_connection).Options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

// See RepositoryTestSupport.CreateSqliteContext's remarks.
internal sealed class SqliteScheduleManagementDbContext(DbContextOptions<ScheduleManagementDbContext> options)
    : ScheduleManagementDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        RepositoryTestSupport.FixModelForSqlite(modelBuilder);
    }
}

// See RepositoryTestSupport.CreateSqliteContext's remarks.
internal sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options)
    : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        RepositoryTestSupport.FixModelForSqlite(modelBuilder);
    }
}

// See RepositoryTestSupport.CreateSqliteContext's remarks.
internal sealed class SqliteAcademicCatalogDbContext(DbContextOptions<AcademicCatalogDbContext> options)
    : AcademicCatalogDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        RepositoryTestSupport.FixModelForSqlite(modelBuilder);
    }
}
