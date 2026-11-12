namespace GlamSpaces.Domain.Entities;

public class FotoSalon
{
    public int Id { get; set; }
    public int SalonId { get; set; }
    public string Url { get; set; } = string.Empty; // URL pública del blob en Azure Storage (HU-07 fotos)
}
