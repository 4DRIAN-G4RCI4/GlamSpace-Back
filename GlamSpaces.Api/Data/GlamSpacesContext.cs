using GlamSpaces.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GlamSpaces.Api.Data;

public class GlamSpacesContext : DbContext
{
    public GlamSpacesContext(DbContextOptions<GlamSpacesContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entidad =>
        {
            entidad.HasIndex(u => u.Correo).IsUnique();
            entidad.Property(u => u.NombreCompleto).IsRequired().HasMaxLength(150);
            entidad.Property(u => u.Correo).IsRequired().HasMaxLength(150);
            entidad.Property(u => u.PasswordHash).IsRequired();
            entidad.Property(u => u.TipoCuenta).IsRequired().HasMaxLength(20);
            entidad.Property(u => u.NombreSalon).HasMaxLength(150);
        });

        base.OnModelCreating(modelBuilder);
    }
}
