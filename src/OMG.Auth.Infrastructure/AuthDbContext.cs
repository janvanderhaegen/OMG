using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OMG.Auth.Infrastructure.Entities;

namespace OMG.Auth.Infrastructure;

public class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("auth");

        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");

            b.Property(u => u.IsEmailVerified)
                .HasDefaultValue(false);

            b.Property(u => u.VerificationCode)
                .HasMaxLength(256);

            b.Property(u => u.VerificationCodeExpiresAt);

            b.Property(u => u.IsDeleted)
                .HasDefaultValue(false);

            b.Property(u => u.DeletedAt);

            b.HasMany(u => u.RefreshTokens)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");

            b.HasKey(t => t.Id);

            b.Property(t => t.TokenHash)
                .IsRequired()
                .HasMaxLength(512);

            b.Property(t => t.CreatedAt)
                .IsRequired();

            b.Property(t => t.ExpiresAt)
                .IsRequired();

            b.Property(t => t.CreatedByIp)
                .HasMaxLength(64);

            b.Property(t => t.RevokedByIp)
                .HasMaxLength(64);

            b.Property(t => t.ReplacedByTokenHash)
                .HasMaxLength(512);

            b.Property(t => t.RevocationReason)
                .HasMaxLength(256);

            b.HasIndex(t => t.UserId);
            b.HasIndex(t => t.TokenHash)
                .IsUnique();
        });
    }
}

