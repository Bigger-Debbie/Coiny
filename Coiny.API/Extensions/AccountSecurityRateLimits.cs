using System.Threading.RateLimiting;
namespace Coiny.API.Extensions;
public static class AccountSecurityRateLimits
{
    public const string Login = "auth-login";
    public const string CodeRecovery = "auth-code-recovery";
    public const string Recovery = "auth-recovery";
    public static IServiceCollection AddAccountSecurityRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options => {
            options.AddPolicy("household-recovery", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.AddPolicy(Login, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.AddPolicy(CodeRecovery, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true }));
            options.AddPolicy(Recovery, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = 3, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true }));
        });
        return services;
    }
}
