namespace GlamSpaces.Api.Dtos;

public class PaqueteResponse
{
    public int Id { get; set; }
    public int SalonId { get; set; }
    public string NombrePaquete { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
}
