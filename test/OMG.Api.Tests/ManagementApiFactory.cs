using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Messaging;
using OMG.Auth.Infrastructure.Messaging;
using OMG.Messaging.Contracts.Auth;

namespace OMG.Api.Tests;

public sealed class ManagementApiFactory : WebApplicationFactory<Program>
{
    public CapturingAuthIntegrationEventPublisher AuthIntegrationEventPublisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var jwtSettings = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "OMG.TestIssuer",
                ["Jwt:Audience"] = "OMG.TestAudience",
                ["Jwt:Secret"] = "super-secret-test-key-1234567890-super-secret-test-key",
                ["Jwt:AccessTokenMinutes"] = "15"
            };

            configBuilder.AddInMemoryCollection(jwtSettings!);
        });

        builder.ConfigureServices(services =>
        {
            // Override integration event publisher with no-op implementation
            services.AddSingleton<IGardenIntegrationEventPublisher, NoOpGardenIntegrationEventPublisher>();

            // Capture auth integration events instead of publishing to a real bus
            services.AddSingleton<IAuthIntegrationEventPublisher>(_ => AuthIntegrationEventPublisher);

            // Remove MassTransit and app hosted services to avoid background work during tests
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();

            foreach (var descriptor in hostedServices)
            {
                services.Remove(descriptor);
            }
        });
    }

    public sealed class CapturingAuthIntegrationEventPublisher : IAuthIntegrationEventPublisher
    {
        public List<SendRegistrationEmail> PublishedMessages { get; } = new();

        public Task PublishRegistrationEmailAsync(
            Guid userId,
            string email,
            string verificationCode,
            CancellationToken cancellationToken = default)
        {
            PublishedMessages.Add(new SendRegistrationEmail(userId, email, verificationCode));
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpGardenIntegrationEventPublisher : IGardenIntegrationEventPublisher
    {
        public Task PublishIntegrationEventsAsync(IEnumerable<OMG.Management.Domain.Abstractions.IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

