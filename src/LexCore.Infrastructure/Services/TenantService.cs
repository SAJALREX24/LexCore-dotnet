using LexCore.Application.Interfaces;

namespace LexCore.Infrastructure.Services;

public class TenantService : ITenantService
{
    private Guid _userId;
    private string _role = string.Empty;

    public Guid GetCurrentUserId() => _userId;
    public string GetCurrentUserRole() => _role;

    public void SetTenantContext(Guid userId, string role)
    {
        _userId = userId;
        _role = role;
    }
}
