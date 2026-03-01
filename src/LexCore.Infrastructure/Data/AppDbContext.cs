using LexCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexCore.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Firm> Firms => Set<Firm>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseLawyer> CaseLawyers => Set<CaseLawyer>();
    public DbSet<CaseClient> CaseClients => Set<CaseClient>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<Hearing> Hearings => Set<Hearing>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Firm Configuration
        modelBuilder.Entity<Firm>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.FirmId);
            entity.HasOne(e => e.Firm).WithMany(f => f.Users).HasForeignKey(e => e.FirmId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Case Configuration
        modelBuilder.Entity<Case>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CaseNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.CaseNumber);
            entity.HasIndex(e => e.FirmId);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Firm).WithMany(f => f.Cases).HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // CaseLawyer Configuration
        modelBuilder.Entity<CaseLawyer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Case).WithMany(c => c.CaseLawyers).HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Lawyer).WithMany(u => u.CaseLawyers).HasForeignKey(e => e.LawyerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.CaseId, e.LawyerId }).IsUnique();
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // CaseClient Configuration
        modelBuilder.Entity<CaseClient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Case).WithMany(c => c.CaseClients).HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Client).WithMany(u => u.CaseClients).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.CaseId, e.ClientId }).IsUnique();
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Document Configuration
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileUrl).IsRequired();
            entity.HasIndex(e => e.FirmId);
            entity.HasOne(e => e.Firm).WithMany(f => f.Documents).HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Case).WithMany(c => c.Documents).HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Uploader).WithMany(u => u.UploadedDocuments).HasForeignKey(e => e.UploadedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // DocumentVersion Configuration
        modelBuilder.Entity<DocumentVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Document).WithMany(d => d.Versions).HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Hearing Configuration
        modelBuilder.Entity<Hearing>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FirmId);
            entity.HasIndex(e => e.HearingDate);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Firm).WithMany(f => f.Hearings).HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Case).WithMany(c => c.Hearings).HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Chat Configuration
        modelBuilder.Entity<Chat>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired();
            entity.HasIndex(e => e.FirmId);
            entity.HasOne(e => e.Firm).WithMany(f => f.Chats).HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Case).WithMany(c => c.Chats).HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Sender).WithMany(u => u.SentChats).HasForeignKey(e => e.SenderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Invoice Configuration
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.GstAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.HasIndex(e => e.FirmId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DueDate);
            entity.HasOne(e => e.Firm).WithMany(f => f.Invoices).HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Case).WithMany(c => c.Invoices).HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Client).WithMany(u => u.ClientInvoices).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Payment Configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasIndex(e => e.FirmId);
            entity.HasOne(e => e.Firm).WithMany().HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Invoice).WithMany(i => i.Payments).HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Subscription Configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FirmId);
            entity.HasOne(e => e.Firm).WithMany(f => f.Subscriptions).HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // AuditLog Configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FirmId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasOne(e => e.Firm).WithMany(f => f.AuditLogs).HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany(u => u.AuditLogs).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Notification Configuration
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Body).IsRequired();
            entity.HasIndex(e => e.FirmId);
            entity.HasOne(e => e.Firm).WithMany(f => f.Notifications).HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany(u => u.Notifications).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
