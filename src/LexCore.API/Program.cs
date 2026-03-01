using Hangfire.Dashboard;
using AspNetCoreRateLimit;
using Hangfire;
using LexCore.API.Extensions;
using LexCore.API.Middleware;
using LexCore.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Listen on all interfaces so physical devices on the same WiFi can connect
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Custom services
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerDocumentation();
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration);

var app = builder.Build();

// Configure pipeline
app.UseMiddleware<ExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LexCore API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseIpRateLimiting();

app.UseCors("LexCorePolicy");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantMiddleware>();

// Serve local files
app.UseStaticFiles();

// Hangfire Dashboard (protected in production)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthFilter() }
});

app.MapControllers();

// Seed database in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync();
}

app.Run();

// Hangfire Dashboard Authorization
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("SuperAdmin");
    }
}
