using BCrypt.Net;
using Microsoft.AspNetCore.Identity;
using OMG.Auth.Infrastructure.Entities;

namespace OMG.Auth.Infrastructure.Security;

/// <summary>
/// ASP.NET Identity password hasher that uses BCrypt for password hashing.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher<ApplicationUser>
{
    public string HashPassword(ApplicationUser user, string password)
    {
        if (password is null)
        {
            throw new ArgumentNullException(nameof(password));
        }

        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user,
        string hashedPassword,
        string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        var verified = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
        return verified ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
    }
}

