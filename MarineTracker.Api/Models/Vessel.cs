using System.ComponentModel.DataAnnotations;

namespace MarineTracker.Api.Models;

public class Vessel
{
    [Key]
    public int Mmsi { get; set; } // AIS базується на MMSI

    public int? ImoNumber { get; set; } // Може бути null, поки не зловимо Static Data

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Flag { get; set; }
    
    public string VesselType { get; set; } = string.Empty;

    public virtual ICollection<VesselPosition> Positions { get; set; } = new List<VesselPosition>();
}