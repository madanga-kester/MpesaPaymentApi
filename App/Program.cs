using System.Text;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Threading.RateLimiting;
using MpesaPaymentApi.Data;
using MpesaPaymentApi.Models.Configuration;
using MpesaPaymentApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Logging;
using Serilog;
using MpesaPaymentApi.Middleware;

var builder = WebApplication.CreateBuilder(args);


IdentityModelEventSource.ShowPII = builder.Environment.IsDevelopment();
IdentityModelEventSource.LogCompleteSecurityArtifact = false; 

builder.Host.UseSerilog((context, services, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

if (builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets<Program>();

builder.Configuration.AddEnvironmentVariables();

Log.Information("Starting up in {Environment} environment", builder.Environment.EnvironmentName);

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
var accessTokenExpiryMinutes = jwtSettings.GetValue("AccessTokenExpiryMinutes", 15);

if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
    throw new InvalidOperationException(
        "JwtSettings:SecretKey is missing or shorter than 32 characters. Set it via user-secrets or environment variables, never in committed config.");

if (string.IsNullOrWhiteSpace(issuer))
    throw new InvalidOperationException("JwtSettings:Issuer is missing or empty.");

if (string.IsNullOrWhiteSpace(audience))
    throw new InvalidOperationException("JwtSettings:Audience is missing or empty.");

var keyBytes = Encoding.UTF8.GetBytes(secretKey);
var signingKey = new SymmetricSecurityKey(keyBytes);


// Services 
builder.Services.Configure<MpesaOptions>(builder.Configuration.GetSection("Mpesa"));
builder.Services.Configure<MpesaPaymentApi.Models.Configuration.ClientAppOptions>(builder.Configuration.GetSection("ClientApps"));
builder.Services.AddHostedService<StalePendingTransactionService>();
builder.Services.AddSingleton<MpesaCallbackQueue>();
builder.Services.AddHostedService<MpesaCallbackQueueProcessor>();

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("MpesaClient", client =>
{
    var baseUrl = builder.Configuration["Mpesa:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Mpesa:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(); 
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            sql.CommandTimeout(30);
        }));


builder.Services.AddScoped<IMpesaService, MpesaService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    
    options.AddPolicy("api", httpContext =>
    {
        var clientId = httpContext.Request.Headers["X-Client-Id"].ToString();
        var partitionKey = !string.IsNullOrWhiteSpace(clientId)
            ? clientId
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 20),
            Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:WindowSeconds", 10)),
            QueueLimit = 0
        });
    });

    options.AddFixedWindowLimiter("callback", limiterOptions =>
    {
        
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sqlserver",
        tags: new[] { "ready" });

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[]
    {
        "http://localhost:4200",
        "http://127.0.0.1:4200",
        "http://localhost:5173",
        "https://linkup254.com",
        "https://www.linkup254.com"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .WithHeaders("Content-Type", "Authorization", "X-Client-Id")
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails(); 
builder.Services.AddHttpContextAccessor();
 
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = signingKey,

            IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) => new[] { signingKey },

            ClockSkew = builder.Environment.IsDevelopment()
                                   ? TimeSpan.FromMinutes(5)
                                   : TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");

                logger.LogWarning(context.Exception, "JWT authentication failed: {ExceptionType}", context.Exception.GetType().Name);

                if (context.Exception is SecurityTokenExpiredException)
                    context.Response.Headers.Append("Token-Expired", "true");

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");
                logger.LogInformation("JWT challenge issued: {Error}", context.Error);

                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        message = "Authentication failed"
                    }));
            },

            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");
                var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                logger.LogDebug("JWT validated for user {UserId}", userId);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("FinanceOps", policy => policy.RequireRole("Admin", "FinanceOps"));


var app = builder.Build();


app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("api");

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false 
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") 
});

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}