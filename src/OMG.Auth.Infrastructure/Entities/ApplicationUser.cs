using Microsoft.AspNetCore.Identity;

namespace OMG.Auth.Infrastructure.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public bool IsEmailVerified { get; set; }

    public string? VerificationCode { get; set; }

    public DateTimeOffset? VerificationCodeExpiresAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

