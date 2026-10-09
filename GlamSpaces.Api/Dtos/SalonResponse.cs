namespace GlamSpaces.Api.Dtos;

// Detalle del salón con sus paquetes y fotos.
public class SalonResponse
{
    public int Id { get; set; }
    public int AdminId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Zona { get; set; } = string.Empty;
    public int Capacidad { get; set; }
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public List<PaqueteResponse> Paquetes { get; set; } = new();
    public List<string> Fotos { get; set; } = new(); // URLs
}
