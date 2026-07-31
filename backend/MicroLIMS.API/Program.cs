using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.Persistence.DbContext;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---- Database ----
builder.Services.AddDbContext<MicroLimsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ---- Application/Infrastructure services (see Extensions/ServiceCollectionExtensions.cs) ----
builder.Services.AddApplicationServices(builder.Configuration);

// ---- JWT Authentication ----
var jwtKey = builder.Configuration["Jwt:Key"]!;
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
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Auto-migrate and seed the first System Administrator + example
    // Item in Development so a fresh clone/DB has something to log into
    // immediately. Remove or guard this differently before production.
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
        db.Database.Migrate();
        MicroLIMS.Persistence.Seed.DbSeeder.Seed(db);
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

app.MapControllers();

app.Run();
