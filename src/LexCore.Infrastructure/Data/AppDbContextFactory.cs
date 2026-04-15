using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LexCore.Infrastructure.Data;

/// <summary>
/// Design-time factory so that `dotnet ef migrations add` can instantiate
/// AppDbContext without needing the API startup project to build.
/// Used only during migration generation — not registered in DI.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=lexcore;Username=postgres;Password=postgres");
        return new AppDbContext(optionsBuilder.Options);
    }
}
