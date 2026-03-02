namespace OMG.Auth.Infrastructure.Options;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string Secret { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;
}

