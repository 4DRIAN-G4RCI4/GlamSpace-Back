namespace GlamSpaces.Api.Models;

public class Salon
{
    public int Id { get; set; }
    public int AdminId { get; set; } // Usuario (TipoCuenta = administrador) dueño del salón
    public string Nombre { get; set; } = string.Empty;
    public string Zona { get; set; } = string.Empty; // ciudad/colonia donde está el salón
    public int Capacidad { get; set; } // número máximo de invitados
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = "no_publicado"; // "publicado" o "no_publicado"
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public Usuario? Admin { get; set; }
    public List<Paquete> Paquetes { get; set; } = new();
    public List<FotoSalon> Fotos { get; set; } = new();
}
