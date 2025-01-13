using Confluent.Kafka;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic; // Для List<>
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Добавляем поддержку Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Home Management Service API", Version = "v1" });
});

var app = builder.Build();

// Включаем Swagger
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Home Management Service API v1"));

// Kafka Consumer
var consumerConfig = new ConsumerConfig
{
    GroupId = "home-management-group",
    BootstrapServers = "kafka:9092",
    AutoOffsetReset = AutoOffsetReset.Earliest
};

// Эндпоинт для управления освещением
app.MapPost("/api/home/lights/{lightId}", (string lightId, HomeManagementService.Models.LightControlRequest request) =>
{
    var response = new
    {
        lightId,
        status = request.Action,
        brightness = request.Brightness
    };
    return Results.Json(response);
}).WithName("ControlLights");

// Эндпоинт для управления отоплением
app.MapPost("/api/home/heating/{zoneId}", (string zoneId, HomeManagementService.Models.HeatingRequest request) =>
{
    var response = new
    {
        zoneId,
        status = "heating",
        setTemperature = request.Temperature
    };
    return Results.Json(response);
}).WithName("SetHeatingTemperature");

// Эндпоинт для подписки на события
app.MapGet("/api/home/consumeUpdates", async (CancellationToken cancellationToken) =>
{
    using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig)
        .SetErrorHandler((_, e) => Console.WriteLine($"Kafka error: {e.Reason}"))
        .Build();

    consumer.Subscribe("telemetry-topic");

    var updates = new List<string>();
    var timeout = TimeSpan.FromSeconds(5); // Тайм-аут ожидания сообщений

    try
    {
        Console.WriteLine("Starting to consume messages...");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = consumer.Consume(timeout);

                if (consumeResult?.Message != null)
                {
                    updates.Add(consumeResult.Message.Value);
                    Console.WriteLine($"Consumed message: {consumeResult.Message.Value}");
                }

                if (updates.Count > 0) // Завершаем, если хотя бы одно сообщение получено
                    break;
            }
            catch (ConsumeException ex)
            {
                Console.WriteLine($"Consume error: {ex.Error.Reason}");
                break; // Прерываем цикл на случай ошибки
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Operation cancelled.");
    }
    finally
    {
        consumer.Close(); // Закрываем потребитель
    }

    if (updates.Count == 0)
    {
        Console.WriteLine("No messages received within the timeout period.");
        return Results.Ok(new { message = "No messages received within the timeout period." });
    }

    return Results.Ok(new { receivedUpdates = updates }); // Возвращаем результат
}).WithName("ConsumeUpdates");

app.Run();
