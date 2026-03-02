using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OMG.Auth.Infrastructure.Entities;
using OMG.Auth.Infrastructure.Options;

namespace OMG.Auth.Infrastructure.Services;

public interface ITokenService
{
    Task<(string accessToken, DateTimeOffset accessTokenExpiresAt, string refreshToken)> CreateTokensAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<(string accessToken, DateTimeOffset accessTokenExpiresAt, string refreshToken)?> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task RevokeAllRefreshTokensAsync(
        Guid userId,
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken);
}

public class TokenService : ITokenService
{
    private readonly AuthDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly byte[] _signingKey;

    public TokenService(
        AuthDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _jwtOptions = jwtOptions.Value;

        if (string.IsNullOrWhiteSpace(_jwtOptions.Secret))
        {
            throw new InvalidOperationException("JWT secret is not configured. Please configure Jwt:Secret in application settings.");
        }

        _signingKey = Encoding.UTF8.GetBytes(_jwtOptions.Secret);
    }

    public async Task<(string accessToken, DateTimeOffset accessTokenExpiresAt, string refreshToken)> CreateTokensAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var (accessToken, expiresAt) = await CreateAccessTokenAsync(user, cancellationToken).ConfigureAwait(false);
        var refreshToken = await CreateRefreshTokenAsync(user, ipAddress, cancellationToken).ConfigureAwait(false);

        return (accessToken, expiresAt, refreshToken);
    }

    public async Task<(string accessToken, DateTimeOffset accessTokenExpiresAt, string refreshToken)?> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var tokenHash = HashToken(refreshToken);

        var existing = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return null;
        }

        if (existing.RevokedAt is not null || existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        var user = existing.User;

        // In-memory provider used in tests does not support real transactions.
        var providerName = _dbContext.Database.ProviderName;
        var isInMemory = providerName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

        string newRefreshToken;

        if (isInMemory)
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            existing.RevokedByIp = ipAddress;
            existing.RevocationReason = "Rotated";

            newRefreshToken = await CreateRefreshTokenAsync(user, ipAddress, cancellationToken).ConfigureAwait(false);
            existing.ReplacedByTokenHash = HashToken(newRefreshToken);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            existing.RevokedAt = DateTimeOffset.UtcNow;
            existing.RevokedByIp = ipAddress;
            existing.RevocationReason = "Rotated";

            newRefreshToken = await CreateRefreshTokenAsync(user, ipAddress, cancellationToken).ConfigureAwait(false);
            existing.ReplacedByTokenHash = HashToken(newRefreshToken);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var (accessToken, expiresAt) = await CreateAccessTokenAsync(user, cancellationToken).ConfigureAwait(false);

        return (accessToken, expiresAt, newRefreshToken);
    }

    public async Task RevokeAllRefreshTokensAsync(
        Guid userId,
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tokens.Count == 0)
        {
            return;
        }

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            token.RevokedByIp = ipAddress;
            token.RevocationReason = reason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string accessToken, DateTimeOffset expiresAt)> CreateAccessTokenAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_jwtOptions.AccessTokenMinutes <= 0 ? 15 : _jwtOptions.AccessTokenMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires.UtcDateTime,
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_signingKey),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = _tokenHandler.WriteToken(token);

        return (accessToken, expires);
    }

    private async Task<string> CreateRefreshTokenAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var rawToken = GenerateSecureRandomToken();
        var tokenHash = HashToken(rawToken);

        var now = DateTimeOffset.UtcNow;
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(30),
            CreatedByIp = ipAddress
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return rawToken;
    }

    private static string GenerateSecureRandomToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}

