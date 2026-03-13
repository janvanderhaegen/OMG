using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMG.Api.Security;
using OMG.Auth.Infrastructure.Entities;
using OMG.Auth.Infrastructure.Services;
using OMG.Auth.Infrastructure.Messaging;
using OMG.Management.Domain.Abstractions;

namespace OMG.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapPost(
                "/register",
                async Task<Results<Created<RegisterResponse>, ValidationProblem>> (
                    [FromServices] UserManager<ApplicationUser> userManager,
                    [FromServices] IAuthIntegrationEventPublisher authIntegrationEventPublisher,
                    [FromServices] IPublishUnitOfWork unitOfWork,
                    [FromServices] IHostEnvironment environment,
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

                    var now = DateTimeOffset.UtcNow;
                    var verificationCode = GenerateVerificationCode();
                    var user = new ApplicationUser
                    {
                        UserName = request.Email,
                        Email = request.Email,
                        EmailConfirmed = false,
                        VerificationCode = verificationCode,
                        VerificationCodeExpiresAt = now.AddMinutes(30),
                        VerificationCodeLastSentAt = now
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

                    var response = environment.IsEnvironment("Testing")
                        ? new RegisterResponse(verificationCode)
                        : new RegisterResponse(null);
                    return TypedResults.Created(string.Empty, response);
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

                    if (!user.EmailConfirmed)
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

        group.MapGet(
                "/verify-email",
                async Task<Results<Ok, ValidationProblem>> (
                    [FromServices] UserManager<ApplicationUser> userManager,
                    [FromQuery] string code,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["code"] = ["Verification code is required."]
                        };

                        return TypedResults.ValidationProblem(errors);
                    }

                    var user = await userManager.Users
                        .SingleOrDefaultAsync(u => u.VerificationCode == code, cancellationToken)
                        .ConfigureAwait(false);

                    if (user is null ||
                        user.VerificationCodeExpiresAt is null ||
                        user.VerificationCodeExpiresAt < DateTimeOffset.UtcNow)
                    {
                        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["code"] = ["Invalid or expired verification code."]
                        };

                        return TypedResults.ValidationProblem(errors);
                    }

                    user.EmailConfirmed = true;
                    user.VerificationCode = null;
                    user.VerificationCodeExpiresAt = null;

                    await userManager.UpdateAsync(user).ConfigureAwait(false);

                    return TypedResults.Ok();
                })
            .WithName("VerifyEmail")
            .WithSummary("Verifies a user's email using a verification code.")
            .WithDescription("Marks a user's email as verified when a valid, non-expired verification code is provided.");

        group.MapPost(
                "/resend-verification-email",
                async Task<Results<NoContent, StatusCodeHttpResult, ValidationProblem>> (
                    [FromServices] UserManager<ApplicationUser> userManager,
                    [FromServices] IAuthIntegrationEventPublisher authIntegrationEventPublisher,
                    [FromServices] IPublishUnitOfWork unitOfWork,
                    [FromBody] ResendVerificationEmailRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var user = await userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
                    if (user is null || user.IsDeleted || user.EmailConfirmed)
                    {
                        return TypedResults.NoContent();
                    }

                    var now = DateTimeOffset.UtcNow;
                    if (user.VerificationCodeLastSentAt is not null &&
                        now - user.VerificationCodeLastSentAt.Value < TimeSpan.FromMinutes(1))
                    {
                        return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
                    }

                    var verificationCode = GenerateVerificationCode();
                    user.VerificationCode = verificationCode;
                    user.VerificationCodeExpiresAt = now.AddMinutes(30);
                    user.VerificationCodeLastSentAt = now;

                    var result = await userManager.UpdateAsync(user).ConfigureAwait(false);
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

                    await authIntegrationEventPublisher
                        .PublishRegistrationEmailAsync(
                            user.Id,
                            user.Email ?? string.Empty,
                            verificationCode,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    return TypedResults.NoContent();
                })
            .WithName("ResendVerificationEmail")
            .WithSummary("Resends the email verification code.")
            .WithDescription("Resends the email verification code, limited to once per minute per user.");

        group.MapDelete(
                "/account",
                async Task<Results<NoContent, UnauthorizedHttpResult>> (
                    [FromServices] UserManager<ApplicationUser> userManager,
                    [FromServices] ITokenService tokenService,
                    [FromServices] IAuthIntegrationEventPublisher authIntegrationEventPublisher,
                    [FromServices] IPublishUnitOfWork unitOfWork,
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

                    var anonymizedToken = Guid.NewGuid().ToString("N");
                    var anonymizedEmail = $"deleted_{anonymizedToken}@example.invalid";
                    var normalizedEmail = anonymizedEmail.ToUpperInvariant();

                    authUser.Email = anonymizedEmail;
                    authUser.NormalizedEmail = normalizedEmail;
                    authUser.UserName = anonymizedEmail;
                    authUser.NormalizedUserName = normalizedEmail;
                    authUser.PhoneNumber = null;
                    authUser.PhoneNumberConfirmed = false;
                    authUser.EmailConfirmed = false;
                    authUser.VerificationCode = null;
                    authUser.VerificationCodeExpiresAt = null;

                    authUser.IsDeleted = true;
                    authUser.DeletedAt = now;

                    await userManager.UpdateAsync(authUser).ConfigureAwait(false);

                    await authIntegrationEventPublisher
                        .PublishUserRemovedAsync(
                            userId.Value,
                            now,
                            "Account deleted by user",
                            cancellationToken)
                        .ConfigureAwait(false);

                    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                    await tokenService
                        .RevokeAllRefreshTokensAsync(userId.Value, ipAddress, "Account deleted", cancellationToken)
                        .ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    return TypedResults.NoContent();
                })
            .WithName("DeleteAccount")
            .WithSummary("Deletes the current user's account and associated gardens.")
            .WithDescription("Marks the user account as deleted, anonymizes personal data, publishes a user-removed event, and revokes refresh tokens.")
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

