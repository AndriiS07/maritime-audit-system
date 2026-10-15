using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace MarineTracker.Api.Services;

public class AisIngestionWorker : BackgroundService
{
    private readonly ILogger<AisIngestionWorker> _logger;
    private readonly string _apiKey;
    private readonly ConnectionFactory _rabbitFactory;

    public AisIngestionWorker(ILogger<AisIngestionWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _apiKey = configuration["AisStream:ApiKey"] ?? throw new ArgumentException("AIS API Key is missing");
        _rabbitFactory = new ConnectionFactory { HostName = configuration.GetConnectionString("RabbitMQ") ?? "localhost" };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var client = new ClientWebSocket();
        
        // Використовуємо асинхронні методи RabbitMQ v7
        using var connection = await _rabbitFactory.CreateConnectionAsync(stoppingToken);
        using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(queue: "ais_messages", durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        try
        {
            await client.ConnectAsync(new Uri("wss://stream.aisstream.io/v0/stream"), stoppingToken);
            _logger.LogInformation("WebSocket підключено. Читання в чергу RabbitMQ...");

            var subscription = new { APIKey = _apiKey, BoundingBoxes = new[] { new[] { new[] { 40.5, 27.0 }, new[] { 47.0, 42.0 } } } };
            var sendBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(subscription));
            await client.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, true, stoppingToken);

            var receiveBuffer = new byte[1024 * 16];
            
            while (!stoppingToken.IsCancellationRequested && client.State == WebSocketState.Open)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), stoppingToken);
                var rawMessage = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);

                var body = Encoding.UTF8.GetBytes(rawMessage);
                
                // Асинхронна публікація
                await channel.BasicPublishAsync(exchange: "", routingKey: "ais_messages", body: body, cancellationToken: stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка Ingestion Worker");
        }
    }
}