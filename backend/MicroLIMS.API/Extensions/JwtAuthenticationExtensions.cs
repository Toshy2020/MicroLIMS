using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MicroLIMS.API.Authorization;

namespace MicroLIMS.API.Extensions;

// JWT bearer authentication, in one place so the exact production setup
// can be exercised end to end by tests (AccessTokenRevalidationTests).
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddMicroLimsJwtAuthentication(this IServiceCollection services, JwtSettings jwtSettings)
    {
        services.AddScoped<AccessTokenRevalidator>();

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
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                // The API both issues and validates these tokens, so there is
                // no clock drift to absorb; the 5-minute default would add a
                // third to a 15-minute token's life.
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            options.Events = new JwtBearerEvents
            {
                // A valid signature says who the user was at issue time;
                // this checks they still are. See AccessTokenRevalidator.
                OnTokenValidated = async context =>
                {
                    var revalidator = context.HttpContext.RequestServices.GetRequiredService<AccessTokenRevalidator>();
                    var reason = await revalidator.GetRejectionReasonAsync(context.Principal!, context.HttpContext.RequestAborted);
                    if (reason is null) return;

                    context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger(typeof(AccessTokenRevalidator))
                        .LogInformation("Rejected a stale access token for user {UserId}: {Reason}",
                            context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, reason);
                    context.Fail(reason);
                }
            };
        });

        return services;
    }
}
