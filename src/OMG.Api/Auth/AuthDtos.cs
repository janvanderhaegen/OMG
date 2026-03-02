using System.ComponentModel.DataAnnotations;

namespace OMG.Api.Auth;

public sealed record RegisterRequest(
    [property: Required]
    [property: EmailAddress]
    string Email,

    [property: Required]
    [property: MinLength(8)]
    string Password);

public sealed record LoginRequest(
    [property: Required]
    [property: EmailAddress]
    string Email,

    [property: Required]
    string Password);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt);

public sealed record RefreshRequest(
    [property: Required]
    string RefreshToken);

