namespace GlamSpaces.Api.Models;

public class FotoSalon
{
    public int Id { get; set; }
    public int SalonId { get; set; }
    public string Url { get; set; } = string.Empty; // URL pública de la imagen (la subida a storage es otra HU)

    public Salon? Salon { get; set; }
}
