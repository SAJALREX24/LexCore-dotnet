using LexCore.Application.Interfaces;

namespace LexCore.Infrastructure.Services;

public class TenantService : ITenantService
{
    private Guid _firmId;
    private Guid _userId;
    private string _role = string.Empty;

    public Guid GetCurrentFirmId() => _firmId;
    public Guid GetCurrentUserId() => _userId;
    public string GetCurrentUserRole() => _role;

    public void SetTenantContext(Guid firmId, Guid userId, string role)
    {
        _firmId = firmId;
        _userId = userId;
        _role = role;
    }
}
