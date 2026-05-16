using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SheepAI.API.Handlers;
using SheepAI.Application.Interfaces.ExternalServices;
using SheepAI.Application.Interfaces.Services;
using SheepAI.Application.Options;
using SheepAI.Infrastructure.ExternalServices;
using SheepAI.Infrastructure.Persistence;
using SheepAI.Infrastructure.Services;
using StackExchange.Redis;

namespace SheepAI.API.StartupExtensions;

public static class ConfigureServicesExtension
{
    public static IServiceCollection ConfigureServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Options — manually bound from flat env var keys
        services.Configure<JwtOptions>(opt =>
        {
            opt.Secret                = config["JWT_SECRET"]             ?? string.Empty;
            opt.Issuer                = config["JWT_ISSUER"]             ?? string.Empty;
            opt.Audience              = config["JWT_AUDIENCE"]           ?? string.Empty;
            opt.ExpiresInMinutes      = config.GetValue<int>("JWT_EXPIRES_MINUTES",      15);
            opt.RefreshTokenExpiryDays = config.GetValue<int>("JWT_REFRESH_EXPIRY_DAYS", 7);
        });
        services.Configure<ClaudeOptions>(opt =>
        {
            opt.ApiKey       = config["CLAUDE_API_KEY"]       ?? string.Empty;
            opt.DefaultModel = config["CLAUDE_DEFAULT_MODEL"] ?? "claude-opus-4-6";
            opt.MaxTokens    = config.GetValue<int>("CLAUDE_MAX_TOKENS", 4096);
        });
        services.Configure<DatabaseOptions>(opt =>
        {
            opt.ConnectionString = config["DATABASE_CONNECTION_STRING"] ?? string.Empty;
        });
        services.AddOptions<CacheTtlOptions>().BindConfiguration(CacheTtlOptions.SectionName);

        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config["DATABASE_CONNECTION_STRING"],
                       o => o.MigrationsHistoryTable("__ef_migrations_history"))
                   .UseSnakeCaseNamingConvention());

        // Cache
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(config["REDIS_CONNECTION"]!));
        services.AddSingleton<ICacheService, CacheService>();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IChatAdminService, ChatAdminService>();
        services.AddScoped<IFileService, FileService>();
        services.AddSingleton<IClaudeService, ClaudeService>();

        // CORS
        var allowedOrigins = new[]
        {
            config["CORS_ALLOWED_ORIGINS_0"],
            config["CORS_ALLOWED_ORIGINS_1"]
        }.Where(o => !string.IsNullOrEmpty(o)).Select(o => o!).ToArray();
        services.AddCors(opt =>
        {
            opt.AddPolicy("Development", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            opt.AddPolicy("Production",  p => p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader());
        });

        // JWT Auth
        var jwtSecret = config["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET is required.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidIssuer              = config["JWT_ISSUER"],
                    ValidateAudience         = true,
                    ValidAudience            = config["JWT_AUDIENCE"],
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                };

                opt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async ctx =>
                    {
                        var cache = ctx.HttpContext.RequestServices.GetRequiredService<ICacheService>();
                        var jti   = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                        if (!string.IsNullOrEmpty(jti))
                        {
                            var blocked = await cache.GetAsync<bool?>($"blocklist:{jti}");
                            if (blocked is true)
                                ctx.Fail("Token has been revoked.");
                        }
                    }
                };
            });
        services.AddAuthorization();

        // Rate limiting — fixed window per IP
        var permitLimit   = config.GetValue<int>("RATE_LIMIT_PERMIT", 20);
        var windowSeconds = config.GetValue<int>("RATE_LIMIT_WINDOW_SECONDS", 60);
        services.AddRateLimiter(opt =>
        {
            opt.AddPolicy("fixed", ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit          = permitLimit,
                        Window               = TimeSpan.FromSeconds(windowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit           = 0
                    }));
            opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        // Controllers
        services.AddControllers()
            .AddJsonOptions(opt =>
            {
                opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
                opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // Exception handling + ProblemDetails
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        // Health checks
        services.AddHealthChecks()
            .AddNpgSql(config["DATABASE_CONNECTION_STRING"] ?? string.Empty)
            .AddRedis(config["REDIS_CONNECTION"] ?? string.Empty);

        // Swagger / OpenAPI
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(opt =>
        {
            opt.SwaggerDoc("v1", new OpenApiInfo { Title = "SheepAI API", Version = "v1" });
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            opt.IncludeXmlComments(xmlPath);
            opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name         = "Authorization",
                Type         = SecuritySchemeType.Http,
                Scheme       = "bearer",
                BearerFormat = "JWT",
                In           = ParameterLocation.Header,
                Description  = "Enter your access token."
            });
            opt.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id   = "Bearer"
                        }
                    },
                    []
                }
            });
        });

        return services;
    }
}
