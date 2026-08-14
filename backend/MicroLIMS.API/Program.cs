using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

// ---- Application/Infrastructure services (see Extensions/ServiceCollectionExtensions.cs) ----
builder.Services.AddApplicationServices(builder.Configuration);

// ---- JWT Authentication ----
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? builder.Configuration["Jwt__Key"]
    ?? "DEV_ONLY_INSECURE_SECRET_KEY_CHANGE_IN_PRODUCTION_MIN_32_CHARS";

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
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "MicroLIMS",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "MicroLIMS.Client",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
    });
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
              .AllowAnyMethod());
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

// ---- Health Check Endpoint ----
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.MapControllers();

app.Run();
