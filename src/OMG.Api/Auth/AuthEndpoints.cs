using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using OMG.Api.Security;
using OMG.Auth.Infrastructure.Entities;
using OMG.Auth.Infrastructure.Services;
using OMG.Auth.Infrastructure.Messaging;
using OMG.Management.Domain.Abstractions;
using OMG.Management.Domain.Gardens;
using OMG.Management.Infrastructure.Messaging;
using OMG.Management.Domain.Common;

namespace OMG.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapPost(
                "/register",
                async Task<Results<Created, ValidationProblem>> (
                    [FromServices] UserManager<ApplicationUser> userManager,
                    [FromServices] IAuthIntegrationEventPublisher authIntegrationEventPublisher,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromBody] RegisterRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var existing = await userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
                    if (existing is not null)
                    {
                        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["email"] = ["A user with this email already exists."]
                        };

                        return TypedResults.ValidationProblem(errors);
                    }

                    var verificationCode = GenerateVerificationCode();
                    var user = new ApplicationUser
                    {
                        UserName = request.Email,
                        Email = request.Email,
                        EmailConfirmed = false,
                        IsEmailVerified = false,
                        VerificationCode = verificationCode,
                        VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
                    };

                    var result = await userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
                    if (!result.Succeeded)
                    {
                        var errors = result.Errors
                            .GroupBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(
                                g => g.Key,
                                g => g.Select(e => e.Description).ToArray(),
                                StringComparer.OrdinalIgnoreCase);

                        return TypedResults.ValidationProblem(errors);
                    }

                    var roleResult = await userManager.AddToRoleAsync(user, "User").ConfigureAwait(false);
                    if (!roleResult.Succeeded)
                    {
                        throw new Exception("Instance wasn't properly configured with correct roles");
                    }

                    await authIntegrationEventPublisher
                        .PublishRegistrationEmailAsync(
                            user.Id,
                            user.Email ?? string.Empty,
                            verificationCode,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return TypedResults.Created(string.Empty);
                })
            .WithName("Register")
            .WithSummary("Registers a new user with email and password.")
            .WithDescription("Registers a new user and triggers an asynchronous email verification flow.");

        group.MapPost(
                "/login",
                async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult, ForbidHttpResult>> (
                    [FromServices] UserManager<ApplicationUser> userManager,
                    [FromServices] ITokenService tokenService,
                    HttpContext httpContext,
                    [FromBody] LoginRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var user = await userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
                    if (user is null || user.IsDeleted)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var passwordValid = await userManager.CheckPasswordAsync(user, request.Password).ConfigureAwait(false);
                    if (!passwordValid)
                    {
                        return TypedResults.Unauthorized();
                    }

                    if (!user.IsEmailVerified)
                    {
                        return TypedResults.Forbid();
                    }

                    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

                    var (accessToken, expiresAt, refreshToken) = await tokenService
                        .CreateTokensAsync(user, ipAddress, cancellationToken)
                        .ConfigureAwait(false);

                    var response = new AuthResponse(accessToken, refreshToken, expiresAt);

                    return TypedResults.Ok(response);
                })
            .WithName("Login")
            .WithSummary("Logs in a user and issues JWT and refresh tokens.")
            .WithDescription("Authenticates a user and returns a short-lived JWT access token and a refresh token.")
            .RequireRateLimiting("login");

        group.MapPost(
                "/refresh",
                async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> (
                    [FromServices] ITokenService tokenService,
                    HttpContext httpContext,
                    [FromBody] RefreshRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

                    var result = await tokenService
                        .RefreshAsync(request.RefreshToken, ipAddress, cancellationToken)
                        .ConfigureAwait(false);

                    if (result is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var (accessToken, expiresAt, refreshToken) = result.Value;
                    var response = new AuthResponse(accessToken, refreshToken, expiresAt);
                    return TypedResults.Ok(response);
                })
            .WithName("RefreshToken")
            .WithSummary("Exchanges a refresh token for a new access token.")
            .WithDescription("Rotates the refresh token and issues a new JWT access token.");

        group.MapPost(
                "/logout",
                async Task<Results<NoContent, UnauthorizedHttpResult>> (
                    [FromServices] ITokenService tokenService,
                    ClaimsPrincipal user,
                    HttpContext httpContext,
                    CancellationToken cancellationToken) =>
                {
                    var userId = user.GetUserId();
                    if (userId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

                    await tokenService
                        .RevokeAllRefreshTokensAsync(userId.Value, ipAddress, "Logout", cancellationToken)
                        .ConfigureAwait(false);

                    return TypedResults.NoContent();
                })
            .WithName("Logout")
            .WithSummary("Logs out the current user by revoking refresh tokens.")
            .WithDescription("Revokes all active refresh tokens for the current user.")
            .RequireAuthorization();

        group.MapDelete(
                "/account",
                async Task<Results<NoContent, UnauthorizedHttpResult>> (
                    [FromServices] UserManager<ApplicationUser> userManager,
                    [FromServices] ITokenService tokenService,
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    ClaimsPrincipal user,
                    HttpContext httpContext,
                    CancellationToken cancellationToken) =>
                {
                    var userId = user.GetUserId();
                    if (userId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var authUser = await userManager.FindByIdAsync(userId.Value.ToString()).ConfigureAwait(false);
                    if (authUser is null)
                    {
                        return TypedResults.NoContent();
                    }

                    var now = DateTimeOffset.UtcNow;

                    authUser.IsDeleted = true;
                    authUser.DeletedAt = now;

                    await userManager.UpdateAsync(authUser).ConfigureAwait(false);

                    var domainUserId = new UserId(userId.Value);

                    var gardens = await gardenRepository
                        .ListByUserAsync(domainUserId, cancellationToken)
                        .ConfigureAwait(false);

                    foreach (var garden in gardens)
                    {
                        garden.MarkDeleted(now);
                        gardenRepository.Remove(garden);
                        await integrationEventPublisher
                            .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                    await tokenService
                        .RevokeAllRefreshTokensAsync(userId.Value, ipAddress, "Account deleted", cancellationToken)
                        .ConfigureAwait(false);

                    return TypedResults.NoContent();
                })
            .WithName("DeleteAccount")
            .WithSummary("Deletes the current user's account and associated gardens.")
            .WithDescription("Marks the user account as deleted, soft-deletes all gardens owned by the user, and revokes refresh tokens.")
            .RequireAuthorization();

        return endpoints;
    }

    private static string GenerateVerificationCode()
    {
        var random = Random.Shared;
        var code = random.Next(0, 999_999);
        return code.ToString("D6");
    }
}

