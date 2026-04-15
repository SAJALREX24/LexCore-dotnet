using Hangfire.Dashboard;
using AspNetCoreRateLimit;
using Hangfire;
using LexCore.Infrastructure.Jobs;
using LexCore.API.Extensions;
using LexCore.API.Middleware;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// URL binding:
// Development: 0.0.0.0 so Flutter on a physical device/emulator
//              on the same WiFi reaches the backend directly.
//              Cloudflare tunnel provides TLS — plain HTTP is acceptable.
// Production:  localhost only. Nginx/reverse proxy handles TLS and
//              forwards internally. JWTs never travel unencrypted
//              on an external network.
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000");
}
else
{
    builder.WebHost.UseUrls("http://localhost:5000");
}

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        // Transform FluentValidation errors into LexCore ApiResponse format
        // so Flutter extractErrorMessage can read data['message'] correctly
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors.Select(x => x.ErrorMessage))
                .ToList();

            var firstError = errors.FirstOrDefault() ?? "Validation failed";
            var result = new Microsoft.AspNetCore.Mvc.ObjectResult(new
            {
                success = false,
                message = firstError,
                errors = errors,
                errorCode = "VALIDATION_ERROR",
                statusCode = 400
            });
            result.StatusCode = 400;
            return result;
        };
    });

// Limit request body size to 10MB (prevents payload DoS attacks)
// Documents use multipart upload which has its own limit
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});
builder.Services.AddEndpointsApiExplorer();

// Custom services
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerDocumentation();
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration);

var app = builder.Build();

// Configure pipeline

// ForwardedHeaders: required when running behind Nginx/Cloudflare/any proxy.
// Without this, the real client IP and HTTPS scheme are invisible to ASP.NET.
// This affects: IP logging in audit trails, RemoteIpAddress in controllers.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});

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

// NOTE: Static files deliberately NOT served for /uploads
// All document access goes through authenticated /api/documents/{id}/download
// which enforces ownership checks before serving files.
// UseStaticFiles() removed to prevent unauthenticated file access.
//
// If you need to serve other static assets (images, icons etc.),
// use a separate wwwroot folder and configure explicitly:
// app.UseStaticFiles(new StaticFileOptions { RequestPath = "/assets" });

// Hangfire Dashboard (protected in production)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// ─────────────────────────────────────────────────────────────
// LexCore Munshi — Automated Notification Jobs
// Time zone: all cron times are UTC
// India (IST) = UTC + 5:30
//   8 PM IST  =  14:30 UTC
//   7 AM IST  =  01:30 UTC
//   9 AM IST  =  03:30 UTC
//  10 AM IST  =  04:30 UTC
// ─────────────────────────────────────────────────────────────

RecurringJob.AddOrUpdate<NotificationJobs>(
    recurringJobId: "evening-hearing-reminders",
    methodCall: job => job.SendEveningHearingReminders(),
    cronExpression: "30 14 * * *",
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<NotificationJobs>(
    recurringJobId: "morning-hearing-list",
    methodCall: job => job.SendMorningHearingList(),
    cronExpression: "30 1 * * *",
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<NotificationJobs>(
    recurringJobId: "limitation-date-alerts",
    methodCall: job => job.SendLimitationAlerts(),
    cronExpression: "30 3 * * *",
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<NotificationJobs>(
    recurringJobId: "overdue-invoice-alerts",
    methodCall: job => job.SendOverdueInvoiceAlerts(),
    cronExpression: "30 4 * * *",
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

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
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        // Development: always allow (for local testing)
        var isDev = httpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment();
        if (isDev) return true;

        // Production: SuperAdmin only
        // Any logged-in lawyer reaching /hangfire is a security risk
        // They can manually trigger or delete scheduled jobs
        return httpContext.User.IsInRole("SuperAdmin");
    }
}
