using MarineTracker.Api.Data;
using MarineTracker.Api.Interfaces;
using MarineTracker.Api.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Підключаємо PostgreSQL + PostGIS
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseNetTopologySuite()
    ));

// 2. Підключаємо Redis як Singleton
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis");
    return ConnectionMultiplexer.Connect(configuration!);
});

// 3. Реєструємо бізнес-логіку для контролерів
builder.Services.AddScoped<IVesselService, VesselService>();

// 4. Реєструємо фонові процеси (RabbitMQ)
builder.Services.AddHostedService<AisIngestionWorker>(); // Producer
builder.Services.AddHostedService<AisProcessingWorker>(); // Consumer

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Порт Vite
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseCors();
app.MapControllers();

app.Run();