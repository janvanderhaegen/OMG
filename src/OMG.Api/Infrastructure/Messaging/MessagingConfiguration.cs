using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OMG.Management.Infrastructure;
using OMG.Telemetrics.Infrastructure.Consumers;

namespace OMG.Api.Infrastructure.Messaging;

public static class MessagingConfiguration
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddEntityFrameworkOutbox<ManagementDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.AddConsumer<PlantAddedConsumer>();
            x.AddConsumer<PlantRemovedConsumer>();
            x.AddConsumer<PlantIdealHumidityLevelChangedConsumer>();
            x.AddConsumer<PlantReclassifiedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var connectionString = configuration.GetConnectionString("rabbitmq");

                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    cfg.Host(connectionString);
                }

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

