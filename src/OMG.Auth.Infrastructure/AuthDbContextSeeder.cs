using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OMG.Auth.Infrastructure.Entities;

namespace OMG.Auth.Infrastructure;

public static class AuthDbContextSeeder
{
    private const string DefaultDemoPassword = "HireMe123!";

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureRoleAsync(roleManager, "User").ConfigureAwait(false);
        await EnsureRoleAsync(roleManager, "Admin").ConfigureAwait(false);

        var bram = await EnsureUserAsync(
                userManager,
                email: "bram@inthepocket.com",
                isVerified: true,
                roles: new[] { "User", "Admin" },
                cancellationToken)
            .ConfigureAwait(false);

        var jonas = await EnsureUserAsync(
                userManager,
                email: "jonas@inthepocket.com",
                isVerified: true,
                roles: new[] { "User" },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole<Guid>> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
        {
            return;
        }

        await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)).ConfigureAwait(false);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        bool isVerified,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = isVerified,
                IsEmailVerified = isVerified
            };

            var result = await userManager.CreateAsync(user, DefaultDemoPassword).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var errorDescriptions = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create demo user '{email}': {errorDescriptions}");
            }
        }
        else if (isVerified && !user.IsEmailVerified)
        {
            user.IsEmailVerified = true;
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user).ConfigureAwait(false);
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role).ConfigureAwait(false))
            {
                await userManager.AddToRoleAsync(user, role).ConfigureAwait(false);
            }
        }

        return user;
    }
}

