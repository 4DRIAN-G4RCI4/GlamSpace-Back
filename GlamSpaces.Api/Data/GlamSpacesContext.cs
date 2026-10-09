using GlamSpaces.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GlamSpaces.Api.Data;

public class GlamSpacesContext : DbContext
{
    public GlamSpacesContext(DbContextOptions<GlamSpacesContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Salon> Salones => Set<Salon>();
    public DbSet<Paquete> Paquetes => Set<Paquete>();
    public DbSet<FotoSalon> FotosSalon => Set<FotoSalon>();

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

        // Salones, Paquetes y FotosSalon ya existen en Azure SQL (script manual);
        // esto solo mapea a esas tablas, no las crea.
        modelBuilder.Entity<Salon>(entidad =>
        {
            entidad.ToTable("Salones", t =>
            {
                t.HasCheckConstraint("CK_Salones_Capacidad", "Capacidad > 0");
                t.HasCheckConstraint("CK_Salones_Estado", "Estado IN ('publicado', 'no_publicado')");
            });
            entidad.Property(s => s.Nombre).IsRequired().HasMaxLength(150);
            entidad.Property(s => s.Zona).IsRequired().HasMaxLength(200);
            entidad.Property(s => s.Descripcion).HasMaxLength(1000);
            entidad.Property(s => s.Estado).IsRequired().HasMaxLength(20);
            entidad.HasOne(s => s.Admin).WithMany().HasForeignKey(s => s.AdminId)
                .HasConstraintName("FK_Salones_Usuarios");
        });

        modelBuilder.Entity<Paquete>(entidad =>
        {
            entidad.ToTable("Paquetes", t => t.HasCheckConstraint("CK_Paquetes_Precio", "Precio > 0"));
            entidad.Property(p => p.NombrePaquete).IsRequired().HasMaxLength(150);
            entidad.Property(p => p.Descripcion).HasMaxLength(500);
            entidad.Property(p => p.Precio).HasColumnType("decimal(10,2)");
            entidad.HasOne(p => p.Salon).WithMany(s => s.Paquetes).HasForeignKey(p => p.SalonId)
                .HasConstraintName("FK_Paquetes_Salones");
        });

        modelBuilder.Entity<FotoSalon>(entidad =>
        {
            entidad.ToTable("FotosSalon");
            entidad.Property(f => f.Url).IsRequired().HasMaxLength(500);
            entidad.HasOne(f => f.Salon).WithMany(s => s.Fotos).HasForeignKey(f => f.SalonId)
                .HasConstraintName("FK_FotosSalon_Salones");
        });

        base.OnModelCreating(modelBuilder);
    }
}
