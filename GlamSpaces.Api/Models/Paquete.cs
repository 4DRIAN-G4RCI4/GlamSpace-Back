namespace GlamSpaces.Api.Models;

public class Paquete
{
    public int Id { get; set; }
    public int SalonId { get; set; }
    public string NombrePaquete { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; } // en pesos, siempre > 0

    public Salon? Salon { get; set; }
}
