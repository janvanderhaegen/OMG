using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OMG.Api.Auth;
using OMG.Auth.Infrastructure.Entities;

namespace OMG.Api.Tests.Auth;

internal sealed record AuthenticatedUser(
    Guid UserId,
    string Email,
    string AccessToken);

internal static class AuthTestHelper
{
    private const string DefaultPassword = "TestPassword123!";

    public static async Task<AuthenticatedUser> AuthenticateAsync(
        ManagementApiFactory factory,
        HttpClient client,
        string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsEmailVerified = true
            };

            var result = await userManager.CreateAsync(user, DefaultPassword).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var errorDescriptions = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create test user '{email}': {errorDescriptions}");
            }
        }
        else if (!user.IsEmailVerified)
        {
            user.IsEmailVerified = true;
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user).ConfigureAwait(false);
        }

        var loginRequest = new LoginRequest(email, DefaultPassword);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>().ConfigureAwait(false)
                           ?? throw new InvalidOperationException("Authentication response was empty.");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

        return new AuthenticatedUser(user.Id, email, authResponse.AccessToken);
    }
}

