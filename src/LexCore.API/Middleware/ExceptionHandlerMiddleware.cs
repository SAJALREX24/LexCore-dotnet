using System.Net;
using System.Text.Json;
using LexCore.Application.DTOs;

namespace LexCore.API.Middleware;

public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = exception switch
        {
            UnauthorizedAccessException _ => new ApiResponse<object>
            {
                Success = false,
                Message = "Unauthorized access",
                Code = "UNAUTHORIZED",
                StatusCode = (int)HttpStatusCode.Unauthorized
            },
            KeyNotFoundException _ => new ApiResponse<object>
            {
                Success = false,
                Message = exception.Message,
                Code = "NOT_FOUND",
                StatusCode = (int)HttpStatusCode.NotFound
            },
            ArgumentException _ => new ApiResponse<object>
            {
                Success = false,
                Message = exception.Message,
                Code = "BAD_REQUEST",
                StatusCode = (int)HttpStatusCode.BadRequest
            },
            InvalidOperationException _ => new ApiResponse<object>
            {
                Success = false,
                Message = exception.Message,
                Code = "INVALID_OPERATION",
                StatusCode = (int)HttpStatusCode.BadRequest
            },
            _ => new ApiResponse<object>
            {
                Success = false,
                Message = "An internal server error occurred",
                Code = "INTERNAL_ERROR",
                StatusCode = (int)HttpStatusCode.InternalServerError
            }
        };

        context.Response.StatusCode = response.StatusCode ?? 500;
        
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
