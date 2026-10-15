using System.Text;
using System.Text.Json;
using MarineTracker.Api.Data;
using MarineTracker.Api.DTOs;
using MarineTracker.Api.Models;
using NetTopologySuite.Geometries;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace MarineTracker.Api.Services;

public class AisProcessingWorker : BackgroundService
{
    private readonly ILogger<AisProcessingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory; 
    private readonly IConnectionMultiplexer _redis;
    private readonly ConnectionFactory _rabbitFactory;
    private IConnection _connection = null!;
    private IChannel _channel = null!;

    public AisProcessingWorker(
        ILogger<AisProcessingWorker> logger,
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _redis = redis;
        _rabbitFactory = new ConnectionFactory { HostName = configuration.GetConnectionString("RabbitMQ") ?? "localhost" };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = await _rabbitFactory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        
        await _channel.QueueDeclareAsync(queue: "ais_messages", durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        // Використовуємо асинхронний консьюмер
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var rawMessage = Encoding.UTF8.GetString(body);
            await ProcessMessageAsync(rawMessage);
        };

        await _channel.BasicConsumeAsync(queue: "ais_messages", autoAck: true, consumer: consumer, cancellationToken: stoppingToken);

        // Утримуємо фоновий процес активним
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

 private async Task ProcessMessageAsync(string rawMessage)
    {
        try
        {
            // 1. Робимо парсинг JSON нечутливим до великих/малих літер
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var message = JsonSerializer.Deserialize<AisMessageDto>(rawMessage, options);
            
            if (message?.MetaData == null || message.MetaData.Mmsi == 0) return;

            var mmsi = message.MetaData.Mmsi;
            var db = _redis.GetDatabase();

            // 2. Змінюємо ключ на banlist, щоб обійти старий "зіпсований" blacklist
            var blacklistKey = $"vessel:banlist:{mmsi}"; 
            if (await db.KeyExistsAsync(blacklistKey)) return;

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var vessel = await dbContext.Vessels.FindAsync(mmsi);

            // Витягуємо курс і швидкість (якщо вони є в повідомленні)
            double course = message.Message?.PositionReport?.Cog ?? 0;
            double speed = message.Message?.PositionReport?.Sog ?? 0;

            if (message.Message?.ShipStaticData != null)
            {
                var staticData = message.Message.ShipStaticData;
                bool isCargoOrTanker = staticData.Type is (>= 70 and <= 79) or (>= 80 and <= 89);

                if (!isCargoOrTanker)
                {
                    if (vessel != null)
                    {
                        dbContext.Vessels.Remove(vessel);
                        await dbContext.SaveChangesAsync();
                        await db.KeyDeleteAsync($"vessel:latest:{mmsi}");
                    }
                    
                    await db.StringSetAsync(blacklistKey, "ignored", TimeSpan.FromHours(24));
                    _logger.LogInformation("Відсіяно судно {Mmsi} (Тип: {Type})", mmsi, staticData.Type);
                    return;
                }

                if (vessel == null)
                {
                    vessel = new Vessel { Mmsi = mmsi, Name = "Unknown", VesselType = "Unknown" };
                    dbContext.Vessels.Add(vessel);
                }

                vessel.Name = staticData.Name?.Trim() ?? vessel.Name;
                vessel.ImoNumber = staticData.ImoNumber > 0 ? staticData.ImoNumber : vessel.ImoNumber;
                vessel.VesselType = staticData.Type is >= 70 and <= 79 ? "Cargo" : "Tanker";
                
                _logger.LogInformation("✅ Знайдено цільове судно: {Name} ({Type}) | Швидкість: {Speed} вузлів", vessel.Name, vessel.VesselType, speed);
            }
            else
            {
                if (vessel == null)
                {
                    vessel = new Vessel 
                    { 
                        Mmsi = mmsi, 
                        Name = string.IsNullOrWhiteSpace(message.MetaData.ShipName) ? "Unknown" : message.MetaData.ShipName.Trim(), 
                        VesselType = "Unknown" 
                    };
                    dbContext.Vessels.Add(vessel);
                }
            }

            // 3. ЗБЕРЕЖЕННЯ КООРДИНАТ ТА ШВИДКОСТІ У БД
            if (message.MetaData.Latitude != 0 && message.MetaData.Longitude != 0)
            {
                var position = new VesselPosition
                {
                    Id = Guid.NewGuid(),
                    VesselMmsi = vessel.Mmsi,
                    Timestamp = DateTime.UtcNow,
                    Location = new Point(message.MetaData.Longitude, message.MetaData.Latitude) { SRID = 4326 },
                    SpeedKnots = speed // Зберігаємо реальну швидкість у БД
                };
                dbContext.VesselPositions.Add(position);
            }

            await dbContext.SaveChangesAsync();

            // 4. ОНОВЛЕННЯ ГАРЯЧОГО КЕШУ В REDIS (Додаємо Course та Speed)
            var hotState = new
            {
                Mmsi = vessel.Mmsi,
                Name = vessel.Name,
                VesselType = vessel.VesselType,
                Latitude = message.MetaData.Latitude,
                Longitude = message.MetaData.Longitude,
                Course = course, // Для повороту іконки на фронтенді
                Speed = speed,   // Для статистики та перевірки на "Підозрілість"
                Timestamp = DateTime.UtcNow
            };
            await db.StringSetAsync($"vessel:latest:{vessel.Mmsi}", JsonSerializer.Serialize(hotState), TimeSpan.FromHours(1));
        }
        catch (JsonException ex) 
        { 
            _logger.LogWarning("Помилка парсингу JSON: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критична помилка під час обробки повідомлення AIS");
        }
    }

    // Допоміжний метод для конвертації AIS-кодів у зрозумілі типи суден
    private string GetVesselTypeName(int typeCode)
    {
        return typeCode switch
        {
            30 => "Fishing",
            >= 60 and <= 69 => "Passenger",
            >= 70 and <= 79 => "Cargo",
            >= 80 and <= 89 => "Tanker",
            >= 40 and <= 49 => "High Speed Craft",
            >= 50 and <= 59 => "Pilot/Tug/Special",
            >= 20 and <= 29 => "Wing in ground",
            _ => "Other/Unknown"
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}