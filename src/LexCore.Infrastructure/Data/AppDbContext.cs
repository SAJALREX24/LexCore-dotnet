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
    public DbSet<CaseNote> CaseNotes => Set<CaseNote>();

    // AI Entities
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiDraft> AiDrafts => Set<AiDraft>();
    public DbSet<AiResearch> AiResearches => Set<AiResearch>();
    public DbSet<AiResearchCache> AiResearchCaches => Set<AiResearchCache>();
    public DbSet<AiAuditLog> AiAuditLogs => Set<AiAuditLog>();
    public DbSet<AiUsageQuota> AiUsageQuotas => Set<AiUsageQuota>();

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
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.BarEnrollmentNumber).HasMaxLength(100);
            entity.Property(e => e.CourtType).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone);
            entity.HasIndex(e => e.FirmId);
            entity.HasOne(e => e.Firm).WithMany(f => f.Users)
                .HasForeignKey(e => e.FirmId).IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Case Configuration
        modelBuilder.Entity<Case>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CaseNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);

            // Existing string fields
            entity.Property(e => e.CaseType).HasMaxLength(100);
            entity.Property(e => e.CourtName).HasMaxLength(300);
            entity.Property(e => e.ClientName).HasMaxLength(200);
            entity.Property(e => e.ClientPhone).HasMaxLength(20);
            entity.Property(e => e.ClientWhatsApp).HasMaxLength(20);
            entity.Property(e => e.ClientPosition).HasMaxLength(100);
            entity.Property(e => e.OppositeParty).HasMaxLength(300);
            entity.Property(e => e.OppositePartyLawyer).HasMaxLength(200);
            entity.Property(e => e.FIRNumber).HasMaxLength(100);
            entity.Property(e => e.CaseStage).HasMaxLength(100);
            entity.Property(e => e.FeeType).HasMaxLength(50);

            // New Screen 1 fields
            entity.Property(e => e.CourtType).HasMaxLength(50);
            entity.Property(e => e.StateUT).HasMaxLength(100);
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.CourtHierarchyName).HasMaxLength(300);
            entity.Property(e => e.CaseTypeCode).HasMaxLength(50);
            entity.Property(e => e.CaseNature).HasMaxLength(50);

            // New Screen 2 fields
            entity.Property(e => e.ClientType).HasMaxLength(20);
            entity.Property(e => e.ClientFatherName).HasMaxLength(200);
            entity.Property(e => e.ClientAddress).HasMaxLength(500);
            entity.Property(e => e.ClientIDDocumentType).HasMaxLength(50);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.CompanyCIN).HasMaxLength(50);
            entity.Property(e => e.CompanyGST).HasMaxLength(20);
            entity.Property(e => e.AuthorisedRepresentative).HasMaxLength(200);
            entity.Property(e => e.AuthorisedRepresentativeDesignation).HasMaxLength(100);

            // New Screen 3 fields
            entity.Property(e => e.OppositeCounselName).HasMaxLength(200);
            entity.Property(e => e.OppositeCounselPhone).HasMaxLength(20);
            entity.Property(e => e.OppositeCounselEnrollment).HasMaxLength(100);
            entity.Property(e => e.OppositeCounselCity).HasMaxLength(100);

            // New Screen 4 fields
            entity.Property(e => e.PSDistrict).HasMaxLength(100);
            entity.Property(e => e.PSState).HasMaxLength(100);
            entity.Property(e => e.NatureOfOffence).HasMaxLength(500);
            entity.Property(e => e.CaseNotesHtml).HasMaxLength(10000);
            entity.Property(e => e.PrivateNotesHtml).HasMaxLength(10000);
            entity.Property(e => e.ClientInstructionsHtml).HasMaxLength(5000);
            entity.Property(e => e.CaseBackground).HasMaxLength(10000);
            entity.Property(e => e.ActsAndSectionsJson).HasMaxLength(20000);
            entity.Property(e => e.OppositePartiesJson).HasMaxLength(20000);

            // New Screen 5 fields
            entity.Property(e => e.PaymentMode).HasMaxLength(20);
            entity.Property(e => e.PaymentReference).HasMaxLength(100);

            // Fee precision
            entity.Property(e => e.AgreedFees).HasPrecision(18, 2);
            entity.Property(e => e.AdvancePaid).HasPrecision(18, 2);
            entity.Property(e => e.PerHearingFee).HasPrecision(18, 2);

            // Indexes
            entity.HasIndex(e => e.CaseNumber)
                  .IsUnique()
                  .HasDatabaseName("IX_Cases_CaseNumber_Unique");
            entity.HasIndex(e => e.FirmId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CourtType);
            entity.HasIndex(e => new { e.FirmId, e.Status })
                  .HasDatabaseName("IX_Cases_FirmId_Status");
            entity.HasIndex(e => new { e.Status, e.LimitationDate })
                  .HasDatabaseName("IX_Cases_Status_LimitationDate")
                  .HasFilter("\"LimitationDate\" IS NOT NULL");

            entity.HasOne(e => e.Firm).WithMany(f => f.Cases)
                  .HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
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

        // CaseNote Configuration
        modelBuilder.Entity<CaseNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Note).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.NoteType).HasMaxLength(50);
            entity.HasOne(e => e.Case).WithMany(c => c.CaseNotes)
                .HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Lawyer).WithMany()
                .HasForeignKey(e => e.LawyerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CaseId);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Document Configuration
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileUrl).IsRequired();
            entity.Property(e => e.MimeType).HasMaxLength(100);
            entity.Property(e => e.Tags).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);

            // New categorisation fields
            entity.Property(e => e.DocumentCategory).HasMaxLength(50);
            entity.Property(e => e.DocumentTag).HasMaxLength(200);
            entity.Property(e => e.DocumentSource).HasMaxLength(50);
            entity.Property(e => e.AIDraftStatus).HasMaxLength(20);

            // Indexes
            entity.HasIndex(e => e.FirmId);
            entity.HasIndex(e => new { e.CaseId, e.DocumentCategory })
                  .HasDatabaseName("IX_Documents_CaseId_Category");

            // FK to Hearing (nullable)
            entity.HasOne(e => e.Hearing).WithMany()
                  .HasForeignKey(e => e.HearingId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Firm).WithMany(f => f.Documents)
                  .HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Case).WithMany(c => c.Documents)
                  .HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Uploader).WithMany(u => u.UploadedDocuments)
                  .HasForeignKey(e => e.UploadedBy).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(e => e.Outcome).HasMaxLength(50);
            entity.Property(e => e.JudgeOrder).HasMaxLength(2000);
            entity.Property(e => e.ActionRequired).HasMaxLength(1000);
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
            entity.HasIndex(e => e.CaseId);       // for case Bills tab queries
            entity.HasIndex(e => e.PaymentDate);  // for analytics queries
            entity.HasIndex(e => new { e.InvoiceNumber, e.FirmId }).IsUnique();
            entity.HasOne(e => e.Firm).WithMany(f => f.Invoices).HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Case).WithMany(c => c.Invoices).HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Client).WithMany(u => u.ClientInvoices).HasForeignKey(e => e.ClientId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.ClientName).HasMaxLength(200);
            entity.Property(e => e.ClientEmail).HasMaxLength(200);
            entity.Property(e => e.ClientPhone).HasMaxLength(50);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // Payment Configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.PaymentMode).HasMaxLength(20);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.PaymentType).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasIndex(e => e.FirmId);
            entity.HasIndex(e => e.InvoiceId);
            entity.HasIndex(e => e.CaseId);
            entity.HasIndex(e => e.PaidAt);

            entity.HasOne(e => e.Firm).WithMany()
                  .HasForeignKey(e => e.FirmId).OnDelete(DeleteBehavior.Cascade);

            // InvoiceId nullable — for advance payments CaseId is set, InvoiceId is null
            entity.HasOne(e => e.Invoice).WithMany(i => i.Payments)
                  .HasForeignKey(e => e.InvoiceId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);

            // CaseId nullable — for invoice payments InvoiceId is set, CaseId is null
            entity.HasOne(e => e.Case).WithMany(c => c.AdvancePayments)
                  .HasForeignKey(e => e.CaseId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);

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
            // INTENTIONALLY NO HasQueryFilter — audit logs are permanent legal evidence
            // They must NEVER be soft-deleted or filtered out under any circumstance
        });

        // Notification Configuration
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasOne(n => n.Lawyer)
                  .WithMany()
                  .HasForeignKey(n => n.LawyerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.Case)
                  .WithMany()
                  .HasForeignKey(n => n.CaseId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.Hearing)
                  .WithMany()
                  .HasForeignKey(n => n.HearingId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);

            // Index for paginated list query — most common query (1M users)
            entity.HasIndex(n => new { n.LawyerId, n.CreatedAt })
                  .HasDatabaseName("IX_Notifications_LawyerId_CreatedAt");

            // Index for unread badge count — called on every app open
            entity.HasIndex(n => new { n.LawyerId, n.IsRead })
                  .HasDatabaseName("IX_Notifications_LawyerId_IsRead");
        });

        // ── Hearings index for Hangfire hearing jobs ────────────────
        // Jobs scan hearings by date every morning (7 AM) and evening (8 PM)
        modelBuilder.Entity<Hearing>(entity =>
        {
            entity.HasIndex(h => h.HearingDate)
                  .HasDatabaseName("IX_Hearings_HearingDate");
        });

        // AiConversation Configuration
        modelBuilder.Entity<AiConversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TenantId);
        });

        // AiMessage Configuration
        modelBuilder.Entity<AiMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConversationId);
            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AiDraft Configuration
        modelBuilder.Entity<AiDraft>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TenantId);
        });

        // AiResearch Configuration
        modelBuilder.Entity<AiResearch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TenantId);
        });

        // AiResearchCache Configuration
        modelBuilder.Entity<AiResearchCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QueryHash).IsUnique();
        });

        // AiAuditLog Configuration
        modelBuilder.Entity<AiAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => e.TenantId);
        });

        // AiUsageQuota Configuration
        modelBuilder.Entity<AiUsageQuota>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            // Unique composite index — one quota row per tenant per month.
            // Prevents duplicate rows from concurrent first-time inserts
            // and makes the DbUpdateException catch in CheckAndIncrement safe.
            entity.HasIndex(e => new { e.TenantId, e.MonthYear })
                  .IsUnique()
                  .HasDatabaseName("IX_AiUsageQuota_TenantId_MonthYear");
            entity.Property(e => e.MonthYear).HasMaxLength(7); // "yyyy-MM"
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
