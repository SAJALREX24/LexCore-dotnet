using System.Security.Cryptography;
using System.Text;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Services;

public class ResearchCacheService : IResearchCacheService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ResearchCacheService> _logger;

    public ResearchCacheService(AppDbContext db, ILogger<ResearchCacheService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CachedResult?> GetCachedResultAsync(string query)
    {
        var hash = ComputeHash(NormalizeQuery(query));

        var cached = await _db.AiResearchCaches
            .FirstOrDefaultAsync(c => c.QueryHash == hash && c.ExpiresAt > DateTime.UtcNow);

        if (cached == null) return null;

        cached.HitCount++;
        cached.LastAccessed = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Research cache hit for query hash {Hash}", hash);
        return new CachedResult(cached.Result, cached.Citations);
    }

    public async Task SaveToCacheAsync(string query, string result, string? citations)
    {
        var normalized = NormalizeQuery(query);
        var hash = ComputeHash(normalized);

        var existing = await _db.AiResearchCaches.FirstOrDefaultAsync(c => c.QueryHash == hash);
        if (existing != null)
        {
            existing.Result = result;
            existing.Citations = citations;
            existing.ExpiresAt = DateTime.UtcNow.AddHours(168);
            existing.LastAccessed = DateTime.UtcNow;
        }
        else
        {
            _db.AiResearchCaches.Add(new AiResearchCache
            {
                QueryHash = hash,
                QueryNormalized = normalized,
                Result = result,
                Citations = citations,
                ExpiresAt = DateTime.UtcNow.AddHours(168)
            });
        }

        await _db.SaveChangesAsync();
    }

    private static string NormalizeQuery(string query)
    {
        return string.Join(" ", query.ToLowerInvariant().Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
