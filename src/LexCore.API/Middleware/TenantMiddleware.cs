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

            if (Guid.TryParse(firmIdClaim, out var firmId) && 
                Guid.TryParse(userIdClaim, out var userId) &&
                !string.IsNullOrEmpty(roleClaim))
            {
                tenantService.SetTenantContext(firmId, userId, roleClaim);
            }
        }

        await _next(context);
    }
}
