using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("postgres_data");

var rabbitMq = builder.AddRabbitMQ("rabbitmq");
builder.AddProject<Projects.OMG_Api>("api")
    .WithSwaggerUI()
    .WithScalar();

builder.Build().Run();
