namespace LexCore.Application.Interfaces;

public record CachedResult(string Result, string? Citations);

public interface IResearchCacheService
{
    Task<CachedResult?> GetCachedResultAsync(string query);
    Task SaveToCacheAsync(string query, string result, string? citations);
}
