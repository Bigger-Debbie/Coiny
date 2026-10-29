using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Coiny.API.Extensions;

public static class HouseholdInvitationRateLimits
{
    public const string JoinPolicy = "household-join";
    public const string GeneratePolicy = "household-invite-generation";
    public static IServiceCollection AddHouseholdInvitationRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options => {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) => {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails {
                    Status = 429, Title = "Too many attempts", Detail = "Please wait before trying again.",
                }, cancellationToken: token);
            };
            // An IP cap complements authenticated-user limits without limiting unrelated API traffic.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api/households/invitations", StringComparison.OrdinalIgnoreCase)
                    ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => Window(60))
                    : RateLimitPartition.GetNoLimiter("other-routes"));
            options.AddPolicy(JoinPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous", _ => Window(5)));
            options.AddPolicy(GeneratePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => Window(5)));
        });
        return services;
    }
    private static FixedWindowRateLimiterOptions Window(int count) => new() {
        PermitLimit = count, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true,
    };
}
