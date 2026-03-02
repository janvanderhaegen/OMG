using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OMG.Api.Auth;
using OMG.Api.Tests.Auth;
using OMG.Auth.Infrastructure;
using OMG.Auth.Infrastructure.Entities;
using OMG.Auth.Infrastructure.Services;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Entities;
using OMG.Messaging.Contracts.Auth;

namespace OMG.Api.Tests;

public class AuthEndpointTests : IClassFixture<ManagementApiFactory>
{
    private readonly ManagementApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointTests(ManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Creates_User_Pending_Verification()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        //seed USER Role
        db.Roles.Add(new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "User", NormalizedName = "USER" });
        await db.SaveChangesAsync();


        var email = "newuser@example.com";

        var request = new RegisterRequest(
            Email: email,
            Password: "StrongPass123!");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);


        var user = await db.Set<ApplicationUser>().SingleOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.False(user!.IsEmailVerified);
        Assert.False(user.EmailConfirmed);
        Assert.NotNull(user.VerificationCode);

        var publishedMessages = _factory.AuthIntegrationEventPublisher.PublishedMessages;
        Assert.Single(publishedMessages);

        var message = publishedMessages.Single();
        Assert.Equal(user.Id, message.UserId);
        Assert.Equal(email, message.Email);
        Assert.Equal(user.VerificationCode, message.VerificationCode);
    }

    [Fact]
    public async Task Login_Fails_When_Not_Verified()
    {
        var email = "unverified@example.com";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                IsEmailVerified = false
            };

            var result = await userManager.CreateAsync(user, "StrongPass123!");
            Assert.True(result.Succeeded);
        }

        var loginRequest = new LoginRequest(email, "StrongPass123!");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_Issues_Tokens_For_Verified_User()
    {
        var user = await AuthTestHelper.AuthenticateAsync(_factory, _client, "verified@example.com");

        Assert.False(string.IsNullOrWhiteSpace(user.AccessToken));
    }

    [Fact]
    public async Task Refresh_Rotates_Refresh_Token()
    {
        var email = "refresh@example.com";

        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, email);

        var initialToken = authenticated.AccessToken;

        // Login again to capture the refresh token issued by the login endpoint
        var loginRequest = new LoginRequest(email, "TestPassword123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>()
                          ?? throw new InvalidOperationException("Auth response was empty.");

        var refreshRequest = new RefreshRequest(authResponse.RefreshToken);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshed = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(authResponse.RefreshToken, refreshed!.RefreshToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var allTokens = await db.RefreshTokens.ToListAsync();
        Assert.Contains(allTokens, t => t.RevokedAt != null);
    }

    [Fact]
    public async Task Logout_Revokes_Refresh_Tokens()
    {
        var email = "logout@example.com";

        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, email);

        var loginRequest = new LoginRequest(email, "TestPassword123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>()
                          ?? throw new InvalidOperationException("Auth response was empty.");

        var logoutResponse = await _client.PostAsync("/api/v1/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == authenticated.UserId)
            .ToListAsync();

        Assert.NotEmpty(tokens);
        Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task DeleteAccount_Anonymizes_User_And_Revokes_Tokens_And_Publishes_UserRemoved()
    {
        var email = "delete-account@example.com";
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, email);
        var userId = authenticated.UserId;

        var response = await _client.DeleteAsync("/api/v1/auth/account");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            Assert.NotNull(user);
            Assert.True(user!.IsDeleted);
            Assert.NotNull(user.DeletedAt);

            // Anonymization: email/username should no longer match the original email
            Assert.NotEqual(email, user.Email);
            Assert.NotEqual(email, user.UserName);
            Assert.False(user.EmailConfirmed);
            Assert.False(user.IsEmailVerified);
            Assert.Null(user.PhoneNumber);
            Assert.Null(user.VerificationCode);
            Assert.Null(user.VerificationCodeExpiresAt);

            var tokens = await authDb.RefreshTokens
                .Where(t => t.UserId == userId)
                .ToListAsync();

            Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
        }

        // Ensure a UserRemoved integration message was published
        var userRemovedMessages = _factory.AuthIntegrationEventPublisher.UserRemovedMessages;
        Assert.Single(userRemovedMessages);
        var message = userRemovedMessages.Single();
        Assert.Equal(userId, message.UserId);
        Assert.NotEqual(default, message.OccurredAt);
    }
}

