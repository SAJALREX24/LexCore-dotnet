using BCrypt.Net;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Data;

public class DataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(AppDbContext context, ILogger<DataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
            
            if (await _context.Users.AnyAsync())
            {
                _logger.LogInformation("Database already seeded.");
                return;
            }

            _logger.LogInformation("Seeding database...");

            // Create Super Admin (no firm)
            var superAdmin = new User
            {
                Id = Guid.NewGuid(),
                FirmId = null,
                Name = "Super Admin",
                Email = "superadmin@lexcore.in",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin@1234", 12),
                Role = UserRole.SuperAdmin,
                IsVerified = true
            };

            // Create Demo Firm
            var demoFirm = new Firm
            {
                Id = Guid.NewGuid(),
                Name = "Demo Law Firm",
                Slug = "demo-law-firm",
                SubscriptionStatus = SubscriptionStatus.Active,
                Plan = SubscriptionPlan.Pro,
                GstNumber = "27AABCU9603R1ZM",
                Address = "123 Legal Street, Mumbai, Maharashtra 400001"
            };

            // Create Firm Admin
            var firmAdmin = new User
            {
                Id = Guid.NewGuid(),
                FirmId = demoFirm.Id,
                Name = "Firm Admin",
                Email = "admin@lexcore.in",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@1234", 12),
                Role = UserRole.FirmAdmin,
                IsVerified = true
            };

            demoFirm.OwnerId = firmAdmin.Id;

            // Create Lawyer
            var lawyer = new User
            {
                Id = Guid.NewGuid(),
                FirmId = demoFirm.Id,
                Name = "Demo Lawyer",
                Email = "lawyer@lexcore.in",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lawyer@1234", 12),
                Role = UserRole.Lawyer,
                IsVerified = true
            };

            // Create Client
            var client = new User
            {
                Id = Guid.NewGuid(),
                FirmId = demoFirm.Id,
                Name = "Demo Client",
                Email = "client@lexcore.in",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Client@1234", 12),
                Role = UserRole.Client,
                IsVerified = true
            };

            // Create Sample Cases
            var case1 = new Case
            {
                Id = Guid.NewGuid(),
                FirmId = demoFirm.Id,
                CaseNumber = $"LEX-{DateTime.UtcNow.Year}-00001",
                Title = "Property Dispute - Sharma vs Kumar",
                Description = "Civil property dispute regarding land ownership in Mumbai",
                CaseType = "Civil",
                CourtName = "Mumbai High Court",
                Status = CaseStatus.Active,
                FiledDate = DateTime.UtcNow.AddMonths(-3),
                InternalNotes = "Key witness identified. Need to prepare cross-examination.",
                ClientVisibleNotes = "Case is progressing well. Next hearing scheduled."
            };

            var case2 = new Case
            {
                Id = Guid.NewGuid(),
                FirmId = demoFirm.Id,
                CaseNumber = $"LEX-{DateTime.UtcNow.Year}-00002",
                Title = "Contract Breach - ABC Corp vs XYZ Ltd",
                Description = "Commercial contract breach case involving service agreements",
                CaseType = "Commercial",
                CourtName = "Delhi District Court",
                Status = CaseStatus.Pending,
                FiledDate = DateTime.UtcNow.AddMonths(-1),
                InternalNotes = "Awaiting documents from opposing counsel.",
                ClientVisibleNotes = "Initial paperwork submitted."
            };

            // Create Case-Lawyer assignments
            var caseLawyer1 = new CaseLawyer { CaseId = case1.Id, LawyerId = lawyer.Id };
            var caseLawyer2 = new CaseLawyer { CaseId = case2.Id, LawyerId = lawyer.Id };

            // Create Case-Client assignments
            var caseClient1 = new CaseClient { CaseId = case1.Id, ClientId = client.Id };
            var caseClient2 = new CaseClient { CaseId = case2.Id, ClientId = client.Id };

            // Create Hearings
            var hearing1 = new Hearing
            {
                Id = Guid.NewGuid(),
                FirmId = demoFirm.Id,
                CaseId = case1.Id,
                HearingDate = DateTime.UtcNow.AddDays(7).Date,
                HearingTime = new TimeSpan(10, 30, 0),
                CourtName = "Mumbai High Court",
                JudgeName = "Hon. Justice A. Patel",
                Notes = "Arguments on land ownership documents",
                Status = HearingStatus.Scheduled
            };

            var hearing2 = new Hearing
            {
                Id = Guid.NewGuid(),
                FirmId = demoFirm.Id,
                CaseId = case2.Id,
                HearingDate = DateTime.UtcNow.AddDays(14).Date,
                HearingTime = new TimeSpan(11, 0, 0),
                CourtName = "Delhi District Court",
                JudgeName = "Hon. Justice B. Singh",
                Notes = "Preliminary hearing for document submission",
                Status = HearingStatus.Scheduled
            };

            // Create Subscription for Demo Firm
            var subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                FirmId = demoFirm.Id,
                Plan = SubscriptionPlan.Pro,
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                EndDate = DateTime.UtcNow.AddMonths(6)
            };

            // Save all entities
            await _context.Firms.AddAsync(demoFirm);
            await _context.Users.AddRangeAsync(superAdmin, firmAdmin, lawyer, client);
            await _context.Cases.AddRangeAsync(case1, case2);
            await _context.CaseLawyers.AddRangeAsync(caseLawyer1, caseLawyer2);
            await _context.CaseClients.AddRangeAsync(caseClient1, caseClient2);
            await _context.Hearings.AddRangeAsync(hearing1, hearing2);
            await _context.Subscriptions.AddAsync(subscription);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Database seeding completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }
}
