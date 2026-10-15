using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace MarineTracker.Api.Models;

public class VesselPosition
{
    [Key]
    public Guid Id { get; set; }

    public int VesselMmsi { get; set; } // Зовнішній ключ тепер вказує на Mmsi
    
    public required Point Location { get; set; } 

    public DateTime Timestamp { get; set; }
    
    public double SpeedKnots { get; set; }

    public virtual Vessel Vessel { get; set; } = null!;
}