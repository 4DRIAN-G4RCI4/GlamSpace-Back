namespace GlamSpaces.Api.Dtos;

// Sirve para agregar (POST /api/salones/{id}/paquetes) y actualizar (PUT /api/paquetes/{id}).
public class PaqueteRequest
{
    // TODO: cuando haya JWT, sacar AdminId del token en vez de recibirlo en el body.
    public int AdminId { get; set; }
    public string NombrePaquete { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
}
