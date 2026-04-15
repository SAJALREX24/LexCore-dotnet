using System.Security.Claims;
using LexCore.Application.Interfaces;

namespace LexCore.API.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantService tenantService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var firmIdClaim = context.User.FindFirst("firmId")?.Value;
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;

            // firmId is optional — individual lawyers have no firm
            if (Guid.TryParse(userIdClaim, out var userId) &&
                !string.IsNullOrEmpty(roleClaim))
            {
                Guid? firmId = Guid.TryParse(firmIdClaim, out var fId) ? fId : (Guid?)null;
                tenantService.SetTenantContext(firmId, userId, roleClaim);
            }
        }

        await _next(context);
    }
}
