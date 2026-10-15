namespace MarineTracker.Api.DTOs;

public class AisMessageDto
{
    public MetaDataDto MetaData { get; set; }
    public AisPayloadDto? Message { get; set; }
}

public class MetaDataDto
{
    public int Mmsi { get; set; }
    public string? ShipName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? TimeUtc { get; set; }
}

public class AisPayloadDto
{
    public ShipStaticDataDto? ShipStaticData { get; set; }
    public PositionReportDto? PositionReport { get; set; } // Додано для швидкості і курсу
}

public class ShipStaticDataDto
{
    public int Type { get; set; }
    public string? Name { get; set; }
    public int ImoNumber { get; set; }
}

public class PositionReportDto
{
    public double Cog { get; set; } // Course Over Ground (Курс у градусах)
    public double Sog { get; set; } // Speed Over Ground (Швидкість у вузлах)
}