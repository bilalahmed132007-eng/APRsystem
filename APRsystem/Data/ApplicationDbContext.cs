using APRsystem.Models;
using APRsystem.Models.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Reflection.Emit;
using System.Text.Json;

namespace APRsystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Contract> Contracts { get; set; }
        public DbSet<KPI> KPIs { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Posting> Postings { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Lookup> Lookups { get; set; }
        public DbSet<PostingKPI> PostingKPIs { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Appraisal> Appraisals { get; set; }
        public DbSet<AppraisalKPI> AppraisalKPIs { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {

            base.OnModelCreating(builder);
            builder.Entity<PostingKPI>()
    .HasOne(pk => pk.Posting)
    .WithMany(p => p.PostingKPIs)
    .HasForeignKey(pk => pk.PostingId)
    .OnDelete(DeleteBehavior.Cascade);

            
            // Employee self-reference (Supervisor)
            builder.Entity<Employee>()
                .HasOne(e => e.Supervisor)
                .WithMany()
                .HasForeignKey(e => e.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Contract -> Employee
            builder.Entity<Contract>()
                .HasOne(c => c.Employee)
                .WithMany(e => e.Contracts)
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Posting -> Employee
            builder.Entity<Posting>()
                .HasOne(p => p.Employee)
                .WithMany(e => e.Postings)
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Posting -> Supervisor (Employee self-ref via Posting)
            builder.Entity<Posting>()
                .HasOne(p => p.Supervisor)
                .WithMany()
                .HasForeignKey(p => p.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Posting -> Contract
            builder.Entity<Posting>()
                .HasOne(p => p.Contract)
                .WithMany(c => c.Postings)
                .HasForeignKey(p => p.ContractId)
                .OnDelete(DeleteBehavior.Restrict);

            // Posting -> Department
            builder.Entity<Posting>()
                .HasOne(p => p.Department)
                .WithMany(d => d.Postings)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Posting -> Designation (Lookup)
            builder.Entity<Posting>()
                .HasOne(p => p.Designation)
                .WithMany()
                .HasForeignKey(p => p.DesignationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Posting -> Location
            builder.Entity<Posting>()
                .HasOne(p => p.Location)
                .WithMany(l => l.Postings)
                .HasForeignKey(p => p.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Posting: Salary precision (avoids silent truncation warning)
            builder.Entity<Posting>()
                .Property(p => p.Salary)
                .HasPrecision(18, 2);

            // Lookup: prevent duplicate Category+Value pairs
            builder.Entity<Lookup>()
                .HasIndex(l => new { l.Category, l.Value })
                .IsUnique();

            // Contract: prevent duplicate contract numbers
            builder.Entity<Contract>()
                .HasIndex(c => c.ContractNumber)
                .IsUnique();
            // AppraisalKPI -> Appraisal
            builder.Entity<AppraisalKPI>()
                .HasOne(ak => ak.Appraisal)
                .WithMany(a => a.AppraisalKPIs)
                .HasForeignKey(ak => ak.AppraisalId)
                .OnDelete(DeleteBehavior.Cascade);

            // Appraisal -> Employee
            builder.Entity<Appraisal>()
                .HasOne(a => a.Employee)
                .WithMany()
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Appraisal -> Posting
            builder.Entity<Appraisal>()
                .HasOne(a => a.Posting)
                .WithMany()
                .HasForeignKey(a => a.PostingId)
                .OnDelete(DeleteBehavior.Restrict);

            // Appraisal -> Supervisor (Employee self-ref via Appraisal)
            builder.Entity<Appraisal>()
                .HasOne(a => a.Supervisor)
                .WithMany()
                .HasForeignKey(a => a.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
            // Entities we never want to audit (avoids noise + avoids Identity table clutter)
        private static readonly HashSet<string> ExcludedEntities = new()
        {
            nameof(AuditLog),
            "IdentityUserToken`1",
            "IdentityUserLogin`1",
            "IdentityUserClaim`1",
            "IdentityUserRole`1",
            "IdentityRoleClaim`1"
        };

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = BuildAuditEntries();

            if (auditEntries.Count > 0)
            {
                AuditLogs.AddRange(auditEntries);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        private List<AuditLog> BuildAuditEntries()
        {
            var httpContext = _httpContextAccessor?.HttpContext;
            var userId = httpContext?.User?.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;
            var userName = httpContext?.User?.Identity?.IsAuthenticated == true
                ? (httpContext.User.Identity!.Name ?? "Unknown")
                : "System";

            var logs = new List<AuditLog>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added &&
                    entry.State != EntityState.Modified &&
                    entry.State != EntityState.Deleted)
                {
                    continue;
                }

                var entityName = entry.Entity.GetType().Name;
                if (ExcludedEntities.Contains(entityName))
                {
                    continue;
                }

                // For Modified entries, skip if nothing actually changed
                if (entry.State == EntityState.Modified &&
                    !entry.Properties.Any(p => p.IsModified && !Equals(p.CurrentValue, p.OriginalValue)))
                {
                    continue;
                }

                var log = new AuditLog
                {
                    UserId = userId,
                    UserName = userName,
                    Timestamp = DateTime.UtcNow,
                    EntityName = entityName,
                    RecordId = GetPrimaryKeyValue(entry),
                    Action = entry.State switch
                    {
                        EntityState.Added => "Created",
                        EntityState.Modified => "Updated",
                        EntityState.Deleted => "Deleted",
                        _ => entry.State.ToString()
                    }
                };

                switch (entry.State)
                {
                    case EntityState.Added:
                        log.NewValues = SerializeValues(entry.Properties, useCurrentValue: true);
                        break;

                    case EntityState.Deleted:
                        log.OldValues = SerializeValues(entry.Properties, useCurrentValue: false);
                        break;

                    case EntityState.Modified:
                        var changedProps = entry.Properties
                            .Where(p => p.IsModified && !Equals(p.CurrentValue, p.OriginalValue))
                            .ToList();

                        log.OldValues = SerializeValues(changedProps, useCurrentValue: false);
                        log.NewValues = SerializeValues(changedProps, useCurrentValue: true);
                        log.ChangedColumns = string.Join(", ", changedProps.Select(p => p.Metadata.Name));
                        break;
                }

                logs.Add(log);
            }

            return logs;
        }

        private static string? GetPrimaryKeyValue(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            if (key == null) return null;

            var values = key.Properties
                .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "null");

            return string.Join(",", values);
        }

        private static string SerializeValues(IEnumerable<PropertyEntry> properties, bool useCurrentValue)
        {
            var dict = properties.ToDictionary(
                p => p.Metadata.Name,
                p => useCurrentValue ? p.CurrentValue : p.OriginalValue);

            return JsonSerializer.Serialize(dict);
        }
    }
}
        
    
