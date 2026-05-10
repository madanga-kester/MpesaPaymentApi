using System.Text;
using System.Security.Cryptography;
using MpesaPaymentApi.Data;
using MpesaPaymentApi.Models.Configuration;
using MpesaPaymentApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Logging;

IdentityModelEventSource.ShowPII = true;
IdentityModelEventSource.LogCompleteSecurityArtifact = true;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

Console.WriteLine($"[CONFIG] ContentRootPath : {builder.Environment.ContentRootPath}");
Console.WriteLine($"[CONFIG] Environment     : {builder.Environment.EnvironmentName}");

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];

Console.WriteLine($"[JWT] SecretKey loaded   : {(string.IsNullOrEmpty(secretKey) ? "❌ NULL / EMPTY" : $"✅ length={secretKey.Length}")}");
Console.WriteLine($"[JWT] Issuer             : {issuer ?? "❌ NULL"}");
Console.WriteLine($"[JWT] Audience           : {audience ?? "❌ NULL"}");


if (string.IsNullOrWhiteSpace(secretKey))
    throw new InvalidOperationException(
        "[FATAL] JwtSettings:SecretKey is missing or empty. " +
        $"Check appsettings.json at: {builder.Environment.ContentRootPath}");

if (string.IsNullOrWhiteSpace(issuer))
    throw new InvalidOperationException("[FATAL] JwtSettings:Issuer is missing or empty.");

if (string.IsNullOrWhiteSpace(audience))
    throw new InvalidOperationException("[FATAL] JwtSettings:Audience is missing or empty.");

var keyBytes = Encoding.UTF8.GetBytes(secretKey);
var fingerprint = Convert.ToHexString(SHA256.HashData(keyBytes))[..8];
Console.WriteLine($"[JWT] Key fingerprint    : {fingerprint} (must match main API)");

var signingKey = new SymmetricSecurityKey(keyBytes) { KeyId = fingerprint };

// Services 
builder.Services.Configure<MpesaOptions>(builder.Configuration.GetSection("Mpesa"));
builder.Services.AddHostedService<StalePendingTransactionService>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("MpesaClient", client =>
{
    var baseUrl = builder.Configuration["Mpesa:BaseUrl"] ?? "https://sandbox.safaricom.co.ke";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure()));
builder.Services.AddScoped<IMpesaService, MpesaService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "http://127.0.0.1:4200",
                "http://localhost:5173",
                "https://linkup254.com",
                "https://www.linkup254.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

//  JWT Authentication 
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
                                   : TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = "Role"
        };





        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var exType = context.Exception.GetType().Name;
                Console.WriteLine($"[JWT FAIL] {exType}: {context.Exception.Message}");

                var inner = context.Exception.InnerException;
                while (inner != null)
                {
                    Console.WriteLine($"[JWT FAIL INNER] {inner.GetType().Name}: {inner.Message}");
                    inner = inner.InnerException;
                }

                if (context.Exception is SecurityTokenExpiredException ex)
                {
                    Console.WriteLine($"[JWT EXPIRED] Token expired at: {ex.Expires} (UTC now: {DateTime.UtcNow})");
                    context.Response.Headers.Append("Token-Expired", "true");
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"[JWT CHALLENGE] error={context.Error} | desc={context.ErrorDescription}");
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        message = "Authentication failed",
                        error = context.Error,
                        errorDescription = context.ErrorDescription
                    }));
            },

            OnTokenValidated = context =>
            {
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var email = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                var firstName = context.Principal?.FindFirst("FirstName")?.Value;
                var lastName = context.Principal?.FindFirst("LastName")?.Value;
                Console.WriteLine($"[JWT OK] Token validated for: {firstName} {lastName} (id={userId}, email={email})");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();


var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("AllowFrontend");
app.UseAuthentication();   
app.UseAuthorization();
app.MapControllers();

app.Run();