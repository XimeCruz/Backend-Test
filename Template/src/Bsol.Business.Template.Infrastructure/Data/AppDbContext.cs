using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bsol.Business.Template.SharedKernel;
using Bsol.Business.Template.SharedKernel.Audit;
using Bsol.Business.Template.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bsol.Business.Template.Infrastructure.Data;

[ExcludeFromCodeCoverage]
public class AppDbContext : DbContext
{
    private readonly IDomainEventDispatcher? _dispatcher;
    private readonly IHttpContextAccessor? _httpContextAccessor;


    public AppDbContext(DbContextOptions<AppDbContext> options,
      IDomainEventDispatcher? dispatcher, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _dispatcher = dispatcher;
        _httpContextAccessor = httpContextAccessor;
    }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }
    public DbSet<Core.TemplateAggregate.Template> Template { get; set; }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    //User For Audits
    public DbSet<Audit> Audits { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Account: AccountNumber único
        modelBuilder.Entity<Account>()
            .HasIndex(a => a.AccountNumber)
            .IsUnique();

        // Transaction: VoucherCode único
        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.VoucherCode)
            .IsUnique();

        // Relaciones Transaction -> Account
        modelBuilder.Entity<Transaction>()
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transaction>()
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        //User For Audits
        var auditEntries = OnBeforeSaveChanges();
        int result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        //User For Audits
        await OnAfterSaveChanges(auditEntries);
        //User For Domaint Events
        await UseDomainEvents();

        return result;
    }

    public override int SaveChanges() => SaveChangesAsync().GetAwaiter().GetResult();

    private async Task UseDomainEvents()
    {
        // ignore events if no dispatcher provided
        if (_dispatcher == null) return;

        // dispatch events only if save was successful
        var entitiesWithEvents = ChangeTracker.Entries<EntityBase>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .ToArray();

        await _dispatcher.DispatchAndClearEvents(entitiesWithEvents);
    }
    private void SetAuditableEntityFields()
    {
        string userName = "Default";
        if (_httpContextAccessor is not null
            && _httpContextAccessor.HttpContext is not null
            && _httpContextAccessor.HttpContext.User is not null
            && _httpContextAccessor.HttpContext.User.Identity is not null
            && _httpContextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            userName = _httpContextAccessor.HttpContext!.User.Identity!.Name ?? "Default";

        }

        foreach (var entry in ChangeTracker
                     .Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = userName;
                    entry.Entity.CreatedAt = DateTime.Now.ToUniversalTime();
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModifiedBy = userName;
                    entry.Entity.LastModified = DateTime.Now.ToUniversalTime();
                    break;
            }
        }
    }

    private List<AuditEntry> OnBeforeSaveChanges()
    {

        SetAuditableEntityFields();
        ChangeTracker.DetectChanges();

        // Filter entries to those that actually changed
        var entries = ChangeTracker
            .Entries<IAuditableEntity>()
            .Where(e => e.State != EntityState.Detached && e.State != EntityState.Unchanged)
            .ToList();

        var auditEntries = new List<AuditEntry>(entries.Count);

        foreach (var entry in entries)
        {
            var auditEntry = CreateAuditEntry(entry);
            ExtractPropertyChanges(auditEntry);
            auditEntries.Add(auditEntry);
        }

        // Only return entries without temporary properties (values generated after save)
        return auditEntries.Where(a => !a.HasTemporaryProperties).ToList();
    }
    private Task OnAfterSaveChanges(List<AuditEntry>? auditEntries)
    {
        if (auditEntries == null || auditEntries.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var auditEntry in auditEntries)
        {
            // Get the final value of the temporary properties
            foreach (var prop in auditEntry.TemporaryProperties)
            {
                if (prop.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                }
                else
                {
                    auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                }
            }

            // Save the Audit entry
            Audits.Add(auditEntry.ToAudit());
        }

        return SaveChangesAsync();
    }


    /// <summary>
    /// Initializes a new AuditEntry for the given EntityEntry.
    /// </summary>
    private static AuditEntry CreateAuditEntry(EntityEntry<IAuditableEntity> entry)
    {
        return new AuditEntry(entry)
        {
            TableName = entry.Metadata.GetTableName()!
        };
    }

    /// <summary>
    /// Populates the AuditEntry with key, old, and new values based on property state.
    /// </summary>
    private static void ExtractPropertyChanges(AuditEntry auditEntry)
    {
        var entry = auditEntry.Entry;

        foreach (var prop in entry.Properties)
        {
            var propName = prop.Metadata.Name;

            if (prop.IsTemporary)
            {
                // DB-generated value; handle after SaveChanges
                auditEntry.TemporaryProperties.Add(prop);
                continue;
            }

            if (prop.Metadata.IsPrimaryKey())
            {
                auditEntry.KeyValues[propName] = prop.CurrentValue;
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditEntry.NewValues[propName] = prop.CurrentValue;
                    break;

                case EntityState.Deleted:
                    auditEntry.OldValues[propName] = prop.OriginalValue;
                    break;

                case EntityState.Modified:
                    if (prop.IsModified)
                    {
                        auditEntry.OldValues[propName] = prop.OriginalValue;
                        auditEntry.NewValues[propName] = prop.CurrentValue;
                    }
                    break;
            }
        }
    }
}
