using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Coiny.API.Controllers;
using Coiny.API.Data;
using Coiny.API.DTOs.Invitations;
using Coiny.API.Extensions;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Coiny.API.Tests;

public class HouseholdInvitationApiTests
{
    [Fact]
    public async Task HttpFlowProtectsOwnersPreviewsWithoutJoiningAndRequiresExplicitConfirmation()
    {
        await using var api = await Api.Create(); var client = api.Client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/households/invitations", null)).StatusCode);
        api.User("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/households/invitations", null)).StatusCode);
        api.User("owner");
        var generated = await client.PostAsync("/api/households/invitations", null); generated.EnsureSuccessStatusCode();
        Assert.True(generated.Headers.CacheControl?.NoStore);
        var invitation = (await generated.Content.ReadFromJsonAsync<GeneratedInvitationResponse>())!;
        var statusText = await client.GetStringAsync("/api/households/invitations"); Assert.DoesNotContain(invitation.Code, statusText);
        api.User("new1");
        var preview = await client.PostAsJsonAsync("/api/households/invitations/preview", new { code = invitation.Code }); preview.EnsureSuccessStatusCode();
        Assert.Equal("Ross household", (await preview.Content.ReadFromJsonAsync<InvitationPreviewResponse>())!.HouseholdName);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/households/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/households/invitations/redeem", new { code = invitation.Code })).StatusCode);
        var join = await client.PostAsJsonAsync("/api/households/invitations/redeem", new { code = invitation.Code, householdId = 1 }); join.EnsureSuccessStatusCode();
        Assert.Contains("Ross household", await client.GetStringAsync("/api/households/me"));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/households", new { name = "Another home" })).StatusCode);
        api.User("new2");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/households/invitations/redeem", new { code = invitation.Code, householdId = 1 })).StatusCode);
    }
    [Fact]
    public async Task PreviewAndRedemptionShareAnAuthenticatedAttemptLimit()
    {
        await using var api = await Api.Create(); api.User("new1");
        for (var i = 0; i < 5; i++) {
            var response = i % 2 == 0
                ? await api.Client.PostAsJsonAsync("/api/households/invitations/preview", new { code = "ZZZZ-ZZZZ" })
                : await api.Client.PostAsJsonAsync("/api/households/invitations/redeem", new { code = "ZZZZ-ZZZZ", householdId = 1 });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        var blocked = await api.Client.PostAsJsonAsync("/api/households/invitations/preview", new { code = "ZZZZ-ZZZZ" });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
    }
    [Fact]
    public async Task RevocationAndHouseholdCreationUseTheSameMembershipRules()
    {
        await using var api = await Api.Create(); api.User("owner");
        var invitation = (await (await api.Client.PostAsync("/api/households/invitations", null)).Content.ReadFromJsonAsync<GeneratedInvitationResponse>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await api.Client.DeleteAsync("/api/households/invitations")).StatusCode);
        api.User("new1");
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.PostAsJsonAsync("/api/households/invitations/preview", new { code = invitation.Code })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.Client.PostAsJsonAsync("/api/households", new { name = " New household " })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.PostAsJsonAsync("/api/households/invitations/preview", new { code = invitation.Code })).StatusCode);
    }

    [Fact]
    public async Task MemberEndpointsAuthorizeRemovalAndEndRemovedMembershipAccess()
    {
        await using var api = await Api.Create(); api.User("member");
        var roster = (await api.Client.GetFromJsonAsync<List<Coiny.API.DTOs.Households.HouseholdMemberResponse>>("/api/households/members"))!;
        var memberId = Assert.Single(roster, m => m.IsCurrentUser).Id;
        var ownerId = Assert.Single(roster, m => m.Role == Coiny.API.Enums.HouseholdRole.Owner).Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.DeleteAsync($"/api/households/members/{ownerId}")).StatusCode);
        api.User("owner");
        Assert.Equal(HttpStatusCode.Conflict, (await api.Client.DeleteAsync($"/api/households/members/{ownerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Client.DeleteAsync($"/api/households/members/{memberId}")).StatusCode);
        api.User("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/households/members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/households/me")).StatusCode);
    }

    private sealed class Api(InvitationTestDatabase database, WebApplication app) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();
        public void User(string id) { Client.DefaultRequestHeaders.Remove("Test-User"); Client.DefaultRequestHeaders.Add("Test-User", id); }
        public static async Task<Api> Create()
        {
            var db = await InvitationTestDatabase.Create(); var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
            builder.Services.AddControllers().AddApplicationPart(typeof(HouseholdInvitationsController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddHttpContextAccessor(); builder.Services.AddHouseholdInvitationRateLimits();
            builder.Services.AddScoped<CoinyContext>(_ => db.Context());
            builder.Services.AddScoped<ICurrentUserService>(services => new InvitationUser(
                services.GetRequiredService<IHttpContextAccessor>().HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!));
            builder.Services.AddScoped<IHouseholdInvitationService>(services => new HouseholdInvitationService(
                services.GetRequiredService<CoinyContext>(), services.GetRequiredService<ICurrentUserService>(), db.Clock));
            builder.Services.AddScoped<IHouseholdService, HouseholdService>();
            builder.Services.AddScoped<Coiny.API.Data.IControlContext>(sp => sp.GetRequiredService<Coiny.API.Data.CoinyContext>());
            builder.Services.AddScoped<Coiny.API.Data.IFinancialContext>(sp => sp.GetRequiredService<Coiny.API.Data.CoinyContext>());

            var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers(); await app.StartAsync();
            return new(db, app);
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); await database.DisposeAsync(); }
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var user = Request.Headers["Test-User"].ToString();
            return Task.FromResult(string.IsNullOrEmpty(user) ? AuthenticateResult.NoResult() : AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], "Test")), "Test")));
        }
    }
}
