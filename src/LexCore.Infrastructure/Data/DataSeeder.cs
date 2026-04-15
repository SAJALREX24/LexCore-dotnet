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

            // v1 solo-only: seed only SuperAdmin. Firm and subscription seeding removed.
            var superAdmin = new User
            {
                Id = Guid.NewGuid(),
                Name = "Super Admin",
                Email = "superadmin@lexcore.in",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin@1234", 12),
                Role = UserRole.SuperAdmin,
                IsVerified = true
            };

            await _context.Users.AddAsync(superAdmin);
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
