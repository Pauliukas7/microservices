using Microsoft.Extensions.DependencyInjection;
using Play.Common.MassTransit;
using Play.Common.MongoDB;
using Play.Inventory.Service.Clients;
using Play.Inventory.Service.Entities;
using Polly;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var services = builder.Services;

services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();
services
    .AddMongo()
    .AddMongoRepository<InventoryItem>("inventoryitems")
    .AddMongoRepository<CatalogItem>("calalogitems")
    .AddMassTransitWithRabbitMq();

AddCatalogClient(services);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void AddCatalogClient(IServiceCollection services)
{
    Random jitterer = new Random();

    services
        .AddHttpClient<CatalogClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7260");
        })
        .AddTransientHttpErrorPolicy(
            builder =>
                builder
                    .Or<TimeoutRejectedException>()
                    .WaitAndRetryAsync(
                        5,
                        retryAttempt =>
                            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                            + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)),
                        onRetry: (outcome, timespan, retryAttempt) =>
                        {
                            var serviceProvider = services.BuildServiceProvider();
                            serviceProvider
                                .GetService<ILogger<CatalogClient>>()
                                ?.LogWarning(
                                    $"Delaying for {timespan.TotalSeconds} seconds, then making retry {retryAttempt}"
                                );
                        }
                    )
        )
        .AddTransientHttpErrorPolicy(
            builder =>
                builder
                    .Or<TimeoutRejectedException>()
                    .CircuitBreakerAsync(
                        3,
                        TimeSpan.FromSeconds(15),
                        onBreak: (outcome, timespan) =>
                        {
                            var serviceProvider = services.BuildServiceProvider();
                            serviceProvider
                                .GetService<ILogger<CatalogClient>>()
                                ?.LogWarning(
                                    $"Opening the circuit for {timespan.TotalSeconds} seconds...."
                                );
                        },
                        onReset: () =>
                        {
                            var serviceProvider = services.BuildServiceProvider();
                            serviceProvider
                                .GetService<ILogger<CatalogClient>>()
                                ?.LogWarning($"Closing the circuit");
                        }
                    )
        )
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(1));
}
