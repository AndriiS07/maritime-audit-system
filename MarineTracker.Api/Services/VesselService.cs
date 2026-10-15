using System.Text.Json;
using MarineTracker.Api.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using MarineTracker.Api.Interfaces;

namespace MarineTracker.Api.Services;

public class VesselService : IVesselService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;

    public VesselService(ApplicationDbContext dbContext, IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _redis = redis;
    }

    public async Task<IEnumerable<object>> GetLivePositionsAsync()
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        
        var keys = server.Keys(pattern: "vessel:latest:*").ToArray();
        if (keys.Length == 0) return new List<object>();

        var values = await db.StringGetAsync(keys);
        
        return values
            .Where(v => v.HasValue)
            .Select(v => JsonSerializer.Deserialize<object>(v.ToString()!))
            .ToList()!;
    }

    public async Task<IEnumerable<object>> GetVesselHistoryAsync(int mmsi, int limit)
    {
        return await _dbContext.VesselPositions
            .Where(vp => vp.VesselMmsi == mmsi)
            .OrderByDescending(vp => vp.Timestamp)
            .Take(limit)
            .Select(vp => new 
            {
                vp.Timestamp,
                Latitude = vp.Location.Y, 
                Longitude = vp.Location.X, 
                vp.SpeedKnots
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetAllVesselsAsync(int page, int pageSize)
    {
        return await _dbContext.Vessels
            .OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new { v.Mmsi, v.ImoNumber, v.Name, v.Flag, v.VesselType })
            .ToListAsync();
    }

    public async Task<object?> GetVesselByMmsiAsync(int mmsi)
    {
        return await _dbContext.Vessels
            .Where(v => v.Mmsi == mmsi)
            .Select(v => new { v.Mmsi, v.ImoNumber, v.Name, v.Flag, v.VesselType })
            .FirstOrDefaultAsync();
    }
}