using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
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

            // Idempotency: only seed if no users exist
            if (await _context.Users.AnyAsync()) return;

            _logger.LogInformation("Seeding database with dev users...");

            var admin = new User
            {
                Name = "Local Admin",
                Email = "admin@lexcore.local",
                Phone = "9999999999",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123", 12),
                Role = UserRole.SuperAdmin,
                IsVerified = true,
                IsPhoneVerified = true,
                CreatedAt = DateTime.UtcNow
            };

            var lawyer = new User
            {
                Name = "Dev Lawyer",
                Email = "lawyer@lexcore.local",
                Phone = "8888888888",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lawyer@123", 12),
                Role = UserRole.Lawyer,
                IsVerified = true,
                IsPhoneVerified = true,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Users.AddRangeAsync(admin, lawyer);
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
