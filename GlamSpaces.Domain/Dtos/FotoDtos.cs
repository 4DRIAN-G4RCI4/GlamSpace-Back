namespace GlamSpaces.Domain.Dtos;

// HU-07 (fotos). La subida llega como multipart/form-data (ver SalonesController.SubirFoto).

// POST /api/salones/eliminarFoto
public class EliminarFotoRequest
{
    public int SalonId { get; set; }
    public int FotoId { get; set; }
    public int AdminId { get; set; } // TODO: tomarlo del JWT cuando exista
}

// Respuesta de subirFoto y eliminarFoto.
public class FotoSalonResponse
{
    public int FotoId { get; set; }
    public string Url { get; set; } = string.Empty;

    public static FotoSalonResponse Desde(FotoSalon f) => new() { FotoId = f.Id, Url = f.Url };
}
