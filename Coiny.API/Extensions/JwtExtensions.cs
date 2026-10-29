using System.Text;
using Coiny.API.Services;
using Coiny.API.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Coiny.API.Extensions;

public static class JwtExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing");

        if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32 || jwtOptions.ExpirationMinutes < 1 || jwtOptions.ExpirationMinutes > 1440)
            throw new InvalidOperationException("JWT requires a key of at least 32 bytes and an expiration between 1 and 1440 minutes.");

        services
            .AddAuthentication(options =>
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
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,

                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                };
                options.Events = new JwtBearerEvents {
                    OnTokenValidated = async context => {
                        var security = context.HttpContext.RequestServices.GetRequiredService<AccountSecurityService>();
                        if (!await security.IsSessionValidAsync(context.Principal!)) context.Fail("Session is no longer valid.");
                    }
                };
            });

        return services;
    }
}