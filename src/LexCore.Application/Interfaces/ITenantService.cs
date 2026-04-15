namespace LexCore.Application.Interfaces;

public interface ITenantService
{
    Guid GetCurrentUserId();
    string GetCurrentUserRole();
    void SetTenantContext(Guid userId, string role);
}
