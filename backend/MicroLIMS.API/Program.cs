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

// ---- Operational logging (see Extensions/LoggingExtensions.cs) ----
// Configured first so startup itself is logged in the chosen format.
builder.AddMicroLimsLogging();

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
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
        // Server-side enforcement of MustChangePassword - see the filter.
        options.Filters.Add<MustChangePasswordFilter>();
    })
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
// Password for the very first System Administrator, supplied out of band
// (user-secrets locally, a hosting secret in production). Absent by
// design: with no value, DbSeeder creates no administrator at all rather
// than one whose password could be read from this repository.
var initialAdminPassword = builder.Configuration["Seed:InitialAdminPassword"]
    ?? builder.Configuration["Seed__InitialAdminPassword"];

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
    MicroLIMS.Persistence.Seed.DbSeeder.Seed(db, initialAdminPassword);
}
else if (applyMigrations)
{
    app.Logger.LogInformation("APPLY_MIGRATIONS is true. Applying pending EF Core database migrations...");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
    db.Database.Migrate();
    MicroLIMS.Persistence.Seed.DbSeeder.Seed(db, initialAdminPassword);
    app.Logger.LogInformation("Database migrations applied successfully.");
}

// A fresh database with no users means no administrator was provisioned,
// because no Seed:InitialAdminPassword was supplied. Say so loudly - the
// deployment is up but nobody can sign in yet.
using (var startupScope = app.Services.CreateScope())
{
    var startupDb = startupScope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
    try
    {
        if (!startupDb.Users.Any())
        {
            app.Logger.LogWarning(
                "No user accounts exist and no initial administrator was created. Set 'Seed:InitialAdminPassword' " +
                "(environment variable 'Seed__InitialAdminPassword') to a password meeting the password policy, then " +
                "restart with migrations/seeding enabled. The account is created with a forced password change. " +
                "No default password is built into this application.");
        }
    }
    catch (Exception ex)
    {
        // The database may be unreachable at startup; readiness reports
        // that. Never let this advisory check stop the process.
        app.Logger.LogDebug(ex, "Could not check whether an administrator account exists at startup.");
    }
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
