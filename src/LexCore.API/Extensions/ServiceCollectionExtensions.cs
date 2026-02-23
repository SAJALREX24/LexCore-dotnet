using System.Text;
using AspNetCoreRateLimit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.Redis.StackExchange;
using LexCore.Application.Interfaces;
using LexCore.Application.Validators;
using LexCore.Infrastructure.Data;
using LexCore.Infrastructure.Jobs;
using LexCore.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

namespace LexCore.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Redis
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));
        }

        // Hangfire
        services.AddHangfire(config =>
        {
            if (!string.IsNullOrEmpty(redisConnection))
            {
                config.UseRedisStorage(redisConnection);
            }
            else
            {
                config.UseInMemoryStorage();
            }
        });
        services.AddHangfireServer();

        // Services
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IStorageService, StorageService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IRazorpayService, RazorpayService>();
        services.AddScoped<DataSeeder>();
        services.AddScoped<HearingReminderJob>();

        // HTTP Client Factory
        services.AddHttpClient("Razorpay");

        // FluentValidation
        services.AddValidatorsFromAssemblyContaining<RegisterFirmRequestValidator>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("FirmAdmin", policy => policy.RequireRole("FirmAdmin", "SuperAdmin"));
            options.AddPolicy("Lawyer", policy => policy.RequireRole("FirmAdmin", "Lawyer", "SuperAdmin"));
            options.AddPolicy("Client", policy => policy.RequireRole("FirmAdmin", "Lawyer", "Client", "SuperAdmin"));
            options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
        });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "LexCore API",
                Version = "v1",
                Description = "Legal Practice Management System API",
                Contact = new OpenApiContact
                {
                    Name = "LexCore Support",
                    Email = "support@lexcore.in"
                }
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.AddInMemoryRateLimiting();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration["Cors:AllowedOrigins"]?.Split(',') ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddPolicy("LexCorePolicy", builder =>
            {
                builder
                    .WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
