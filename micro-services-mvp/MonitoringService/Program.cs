using Confluent.Kafka;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;

var builder = WebApplication.CreateBuilder(args);

// Добавляем поддержку Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Monitoring Service API", Version = "v1" });
});

var app = builder.Build();

// Включаем Swagger
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Monitoring Service API v1"));

// Kafka Producer
var producerConfig = new ProducerConfig { BootstrapServers = "kafka:9092" };
using var producer = new ProducerBuilder<Null, string>(producerConfig).Build();

// Эндпоинт для получения текущей телеметрии
app.MapGet("/api/monitoring/{deviceId}/telemetry", (string deviceId) =>
{
    var response = new
    {
        deviceId,
        temperature = 22.5,
        humidity = 45.2,
        status = "online",
        timestamp = DateTime.UtcNow.ToString("o")
    };
    return Results.Json(response);
}).WithName("GetTelemetry");

// Эндпоинт для загрузки исторических данных телеметрии
app.MapPost("/api/monitoring/{deviceId}/history", (string deviceId, MonitoringService.Models.DateRange range) =>
{
    var history = new[]
    {
        new { timestamp = "2025-01-01T00:05:00Z", temperature = 22.0, humidity = 44.0 },
        new { timestamp = "2025-01-01T00:10:00Z", temperature = 22.1, humidity = 44.5 }
    };
    return Results.Json(history);
}).WithName("GetTelemetryHistory");

// Эндпоинт для публикации данных телеметрии в Kafka
app.MapPost("/api/monitoring/{deviceId}/publishTelemetry", async (string deviceId) =>
{
    var telemetryData = new
    {
        deviceId,
        temperature = 22.5,
        humidity = 45.2,
        status = "online",
        timestamp = DateTime.UtcNow.ToString("o")
    };

    var message = System.Text.Json.JsonSerializer.Serialize(telemetryData);

    try
    {
        await producer.ProduceAsync("telemetry-topic", new Message<Null, string> { Value = message });
        Console.WriteLine($"Message published: {message}");
        return Results.Ok(new { status = "Message published to Kafka", telemetryData });
    }
    catch (ProduceException<Null, string> ex)
    {
        Console.WriteLine($"Error publishing message: {ex.Error.Reason}");
        return Results.Problem($"Error publishing message: {ex.Error.Reason}");
    }
}).WithName("PublishTelemetry");

app.Run();
