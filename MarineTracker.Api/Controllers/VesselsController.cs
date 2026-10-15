using Microsoft.AspNetCore.Mvc;
using MarineTracker.Api.Interfaces;

namespace MarineTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VesselsController : ControllerBase
{
    private readonly IVesselService _vesselService;

    public VesselsController(IVesselService vesselService)
    {
        _vesselService = vesselService;
    }

    [HttpGet("live")]
    public async Task<IActionResult> GetLivePositions()
    {
        var positions = await _vesselService.GetLivePositionsAsync();
        return Ok(positions);
    }

    [HttpGet("{mmsi}/history")]
    public async Task<IActionResult> GetVesselHistory(int mmsi, [FromQuery] int limit = 100)
    {
        // Перевіряємо існування судна безпосередньо через сервіс
        var vessel = await _vesselService.GetVesselByMmsiAsync(mmsi);
        if (vessel == null) return NotFound($"Судно з MMSI {mmsi} не знайдено");

        var history = await _vesselService.GetVesselHistoryAsync(mmsi, limit);
        return Ok(history);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllVessels([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var vessels = await _vesselService.GetAllVesselsAsync(page, pageSize);
        return Ok(vessels);
    }

    [HttpGet("{mmsi}")]
    public async Task<IActionResult> GetVesselByMmsi(int mmsi)
    {
        var vessel = await _vesselService.GetVesselByMmsiAsync(mmsi);
        if (vessel == null) return NotFound();

        return Ok(vessel);
    }
}