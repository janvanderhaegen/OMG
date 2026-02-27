using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Messaging;

namespace OMG.Api.Tests;

public sealed class ManagementApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Override integration event publisher with no-op implementation
            services.AddSingleton<IGardenIntegrationEventPublisher, NoOpGardenIntegrationEventPublisher>();

            // Optionally remove MassTransit hosted services if present
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            d.ImplementationType?.Namespace is not null &&
                            d.ImplementationType.Namespace.Contains("MassTransit", StringComparison.Ordinal))
                .ToList();

            foreach (var descriptor in hostedServices)
            {
                services.Remove(descriptor);
            }
        });
    }

    private sealed class NoOpGardenIntegrationEventPublisher : IGardenIntegrationEventPublisher
    {
        public Task PublishIntegrationEventsAsync(IEnumerable<OMG.Management.Domain.Abstractions.IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

