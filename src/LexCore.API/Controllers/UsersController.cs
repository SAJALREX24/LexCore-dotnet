using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Users;
using LexCore.Application.Interfaces;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IAuditService _auditService;

    public UsersController(AppDbContext context, ITenantService tenantService, IAuditService auditService)
    {
        _context = context;
        _tenantService = tenantService;
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<List<UserListDto>>>> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var query = _context.Users
            .Where(u => u.FirmId == firmId)
            .OrderByDescending(u => u.CreatedAt);

        var totalCount = await query.CountAsync();
        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsVerified = u.IsVerified,
                LastLogin = u.LastLogin,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(new PagedResponse<UserListDto>
        {
            Data = users,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> GetUser(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var currentUserId = _tenantService.GetCurrentUserId();
        var currentRole = _tenantService.GetCurrentUserRole();

        var user = await _context.Users
            .Where(u => u.Id == id && u.FirmId == firmId)
            .Select(u => new UserDetailDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsVerified = u.IsVerified,
                LastLogin = u.LastLogin,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                AssignedCasesCount = u.Role == UserRole.Lawyer 
                    ? u.CaseLawyers.Count(cl => cl.DeletedAt == null)
                    : u.CaseClients.Count(cc => cc.DeletedAt == null)
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(ApiResponse<UserDetailDto>.ErrorResponse("User not found", "USER_NOT_FOUND", 404));
        }

        if (currentRole == UserRole.Client.ToString() && id != currentUserId)
        {
            return Forbid();
        }

        return Ok(ApiResponse<UserDetailDto>.SuccessResponse(user));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var currentUserId = _tenantService.GetCurrentUserId();
        var currentRole = _tenantService.GetCurrentUserRole();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.FirmId == firmId);

        if (user == null)
        {
            return NotFound(ApiResponse<UserDetailDto>.ErrorResponse("User not found", "USER_NOT_FOUND", 404));
        }

        if (currentRole != UserRole.FirmAdmin.ToString() && currentRole != UserRole.SuperAdmin.ToString() && id != currentUserId)
        {
            return Forbid();
        }

        if (!string.IsNullOrEmpty(request.Name))
            user.Name = request.Name;

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
            {
                return BadRequest(ApiResponse<UserDetailDto>.ErrorResponse("Email already in use", "EMAIL_EXISTS", 400));
            }
            user.Email = request.Email;
        }

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("USER_UPDATED", "User", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<UserDetailDto>.SuccessResponse(new UserDetailDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsVerified = user.IsVerified,
            LastLogin = user.LastLogin,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        }));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var currentUserId = _tenantService.GetCurrentUserId();

        if (id == currentUserId)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Cannot delete your own account", "CANNOT_DELETE_SELF", 400));
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.FirmId == firmId);

        if (user == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("User not found", "USER_NOT_FOUND", 404));
        }

        user.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("USER_DELETED", "User", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "User deleted successfully"));
    }
}
