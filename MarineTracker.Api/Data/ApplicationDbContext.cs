using MarineTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MarineTracker.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Vessel> Vessels { get; set; }
    public DbSet<VesselPosition> VesselPositions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Активуємо розширення PostGIS для бази даних
        modelBuilder.HasPostgresExtension("postgis");

        // Налаштовуємо зв'язок один-до-багатьох
        modelBuilder.Entity<VesselPosition>()
            .HasOne(vp => vp.Vessel)
            .WithMany(v => v.Positions)
            .HasForeignKey(vp => vp.VesselMmsi); // Замінили VesselImoNumber на VesselMmsi
    }
}