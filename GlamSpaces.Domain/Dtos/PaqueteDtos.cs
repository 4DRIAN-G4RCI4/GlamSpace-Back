namespace GlamSpaces.Domain.Dtos;

public class PaqueteRequest
{
    // TODO: cuando haya JWT, sacar AdminId del token en vez de recibirlo en el body.
    public int AdminId { get; set; }
    public string NombrePaquete { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
}

// POST /api/paquetes/crear
public class CrearPaqueteRequest : PaqueteRequest
{
    public int SalonId { get; set; }
}

// POST /api/paquetes/actualizar
public class ActualizarPaqueteRequest : PaqueteRequest
{
    public int Id { get; set; }
}

// POST /api/paquetes/eliminar
public class EliminarPaqueteRequest
{
    public int Id { get; set; }
    // TODO: cuando haya JWT, sacar AdminId del token en vez de recibirlo en el body.
    public int AdminId { get; set; }
}

public class PaqueteResponse
{
    public int Id { get; set; }
    public int SalonId { get; set; }
    public string NombrePaquete { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }

    public static PaqueteResponse Desde(Paquete p) => new()
    {
        Id = p.Id,
        SalonId = p.SalonId,
        NombrePaquete = p.NombrePaquete,
        Descripcion = p.Descripcion,
        Precio = p.Precio,
    };
}
