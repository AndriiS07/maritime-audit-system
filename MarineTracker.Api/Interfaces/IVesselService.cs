namespace MarineTracker.Api.Interfaces;

public interface IVesselService
{
    Task<IEnumerable<object>> GetLivePositionsAsync();
    Task<IEnumerable<object>> GetVesselHistoryAsync(int mmsi, int limit);
    Task<IEnumerable<object>> GetAllVesselsAsync(int page, int pageSize);
    Task<object?> GetVesselByMmsiAsync(int mmsi);
}