using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Json;
using MicroLIMS.Persistence.DbContext;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---- Dynamic Port Binding for Render / Cloud Hosting ----
var hostPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(hostPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{hostPort}");
}

// ---- Forwarded Headers (Reverse Proxy / Render / HTTPS support) ----
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---- Database Connection ----
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["ConnectionStrings:Default"]
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"];

builder.Services.AddDbContext<MicroLimsDbContext>(options =>
    options.UseNpgsql(connectionString));

// ---- JWT configuration ----
// Resolved and validated before any service is registered, so a
// deployment without a real signing key fails at startup rather than at
// the first login. Registered as a singleton because the signing side
// (JwtTokenService) resolves the same instance - there is deliberately
// no second read of Jwt:Key anywhere.
var jwtSettings = JwtConfiguration.Resolve(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(jwtSettings);

// ---- Application/Infrastructure services (see Extensions/ServiceCollectionExtensions.cs) ----
builder.Services.AddApplicationServices(builder.Configuration);

// ---- Liveness / readiness probes (see Extensions/HealthCheckExtensions.cs) ----
builder.Services.AddMicroLimsHealthChecks();

// ---- JWT Authentication ----
builder.Services.AddAuthentication(options =>
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
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
    };
});

builder.Services.AddAuthorization();
// Permission-based authorization, running alongside the existing role-
// string [Authorize(Roles=...)] system - not replacing it in this phase.
// [Authorize(Policy = "<permission code>")] resolves dynamically via
// PermissionPolicyProvider, no per-code AddPolicy() call needed.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
    });
// Protects the anonymous client-error endpoint (see RateLimitingExtensions).
builder.Services.AddMicroLimsRateLimiting(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- CORS Configuration (Supports local dev & Cloudflare Pages) ----
var rawFrontendOrigins = builder.Configuration["Frontend:Origin"]
    ?? builder.Configuration["Frontend__Origin"]
    ?? "http://localhost:5173";

var allowedOrigins = rawFrontendOrigins
    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              // AllowAnyHeader covers request headers only. Without this
              // the browser hides X-Correlation-Id from JS entirely, and
              // client-side error reports cannot be tied to the backend
              // error from the same action.
              .WithExposedHeaders(MicroLIMS.API.Middleware.CorrelationIdMiddleware.HeaderName));
});

var app = builder.Build();

app.UseForwardedHeaders();

// Smtp warning if unconfigured
if (string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"]))
{
    app.Logger.LogWarning("Smtp:Host is not configured - password reset emails will not actually be sent. Set the Smtp section in appsettings to enable delivery.");
}

// ---- Database Migrations & Seeding ----
var applyMigrations = builder.Configuration.GetValue<bool>("APPLY_MIGRATIONS") ||
    string.Equals(Environment.GetEnvironmentVariable("APPLY_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Auto-migrate and seed the first System Administrator + example
    // Item in Development so a fresh clone/DB has something to log into immediately.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
    db.Database.Migrate();
    MicroLIMS.Persistence.Seed.DbSeeder.Seed(db);
}
else if (applyMigrations)
{
    app.Logger.LogInformation("APPLY_MIGRATIONS is true. Applying pending EF Core database migrations...");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
    db.Database.Migrate();
    MicroLIMS.Persistence.Seed.DbSeeder.Seed(db);
    app.Logger.LogInformation("Database migrations applied successfully.");
}

app.UseCors("Frontend");

// Global exception handling + request logging (safe to run before auth).
app.UseMicroLimsEarlyPipeline();

app.UseAuthentication();
app.UseAuthorization();

// Stamps the current user onto the DbContext for audit trail capture -
// must run after UseAuthentication so HttpContext.User is populated.
app.UseMicroLimsAuditPipeline();

app.UseRateLimiter();

// ---- Health Check Endpoints ----
// /health is liveness (process alive), /health/ready is readiness
// (PostgreSQL reachable and schema current). See HealthCheckExtensions.
app.MapMicroLimsHealthChecks();

app.MapControllers();

app.Run();
