namespace GlamSpaces.Api.Dtos;

// Sirve para crear (POST) y actualizar (PUT) un salón.
// Al crear, Estado se ignora: el salón siempre nace "no_publicado".
public class SalonRequest
{
    // TODO: cuando haya JWT, sacar AdminId del token en vez de recibirlo en el body.
    public int AdminId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Zona { get; set; } = string.Empty;
    public int Capacidad { get; set; }
    public string? Descripcion { get; set; }
    public string? Estado { get; set; } // "publicado" o "no_publicado"; solo se usa en PUT
}
