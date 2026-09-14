using Domain.Entities;
using Focus_Be.Extensions;
using Infrastructure.FocusDbContext;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Suppress database writes: these checks exercise EF's save pipeline, not PostgreSQL.
const string connectionString = "Host=localhost;Database=focus_verification;Username=unused";
int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"FAIL: {name}");
    checks++;
    Console.WriteLine($"PASS: {name}");
}

foreach (bool asynchronous in new[] { false, true })
{
    var options = new DbContextOptionsBuilder<ProbeContext>()
        .UseNpgsql(connectionString)
        .AddInterceptors(new TrackingInterceptor(), new SuppressWrites())
        .Options;
    using var db = new ProbeContext(options);
    var old = DateTimeOffset.UtcNow.AddDays(-1);
    var added = new Probe { Id = Guid.NewGuid(), CreatedAt = old, UpdatedAt = old };
    var modified = new Probe { Id = Guid.NewGuid(), CreatedAt = old, UpdatedAt = old };
    var unchanged = new Probe { Id = Guid.NewGuid(), CreatedAt = old, UpdatedAt = old };
    var deleted = new Probe { Id = Guid.NewGuid(), CreatedAt = old, UpdatedAt = old };
    db.AttachRange(modified, unchanged, deleted);
    modified.Name = "changed";
    db.Remove(deleted);
    db.Add(added);
    var before = DateTimeOffset.UtcNow;
    if (asynchronous) await db.SaveChangesAsync();
    else db.SaveChanges();
    var after = DateTimeOffset.UtcNow;
    string mode = asynchronous ? "async" : "sync";
    Check(added.CreatedAt >= before && added.CreatedAt <= after &&
          added.CreatedAt.Offset == TimeSpan.Zero && added.UpdatedAt == added.CreatedAt,
          $"{mode}: inserted timestamps are equal and UTC");
    Check(modified.CreatedAt == old && modified.UpdatedAt >= before && modified.UpdatedAt <= after,
          $"{mode}: property change is detected; creation timestamp preserved");
    Check(unchanged.CreatedAt == old && unchanged.UpdatedAt == old,
          $"{mode}: unchanged entity keeps timestamps");
    Check(deleted.CreatedAt == old && deleted.UpdatedAt == old,
          $"{mode}: deleted entity keeps timestamps");
}

var config = new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string, string?> { ["ConnectionStrings:FocusDb"] = connectionString }).Build();
var services = new ServiceCollection();
services.AddDatabaseService(config);
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
using var scope = provider.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
Check(context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL",
      "DI: PostgreSQL provider resolves");
Check(ReferenceEquals(context, scope.ServiceProvider.GetRequiredService<FocusDbContext>()),
      "DI: DbContext is scoped");
var registeredOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<FocusDbContext>>();
Check(registeredOptions.Extensions.OfType<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>()
      .SelectMany(extension => extension.Interceptors ?? []).Any(i => i is TrackingInterceptor),
      "DI: tracking interceptor is registered on context");
bool missingRejected = false;
try { new ServiceCollection().AddDatabaseService(new ConfigurationBuilder().Build()); }
catch (InvalidOperationException ex) { missingRejected = ex.Message.Contains("ConnectionStrings:FocusDb"); }
Check(missingRejected, "DI: missing connection string produces actionable error");
Console.WriteLine($"Completed: {checks} checks passed. No database writes performed.");

sealed class Probe : BaseEntity
{
    public string Name { get; set; } = "original";
}

sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Probe>();
}

sealed class SuppressWrites : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result) => InterceptionResult<int>.SuppressWithResult(0);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
}
