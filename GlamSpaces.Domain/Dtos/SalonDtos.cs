namespace GlamSpaces.Domain.Dtos;

// POST /api/salones/crear. El salón siempre nace "no_publicado".
public class SalonRequest
{
    // TODO: cuando haya JWT, sacar AdminId del token en vez de recibirlo en el body.
    public int AdminId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Zona { get; set; } = string.Empty;
    public int Capacidad { get; set; }
    public string? Descripcion { get; set; }
}

// POST /api/salones/actualizar. Si Estado viene null se conserva el actual.
public class ActualizarSalonRequest : SalonRequest
{
    public int Id { get; set; }
    public string? Estado { get; set; } // "publicado" o "no_publicado"
}

// POST /api/salones/listar
public class ListarSalonesRequest : PaginacionRequest
{
    public int? AdminId { get; set; } // null = todos los salones
}

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

    public static SalonResponse Desde(Salon s) => new()
    {
        Id = s.Id,
        AdminId = s.AdminId,
        Nombre = s.Nombre,
        Zona = s.Zona,
        Capacidad = s.Capacidad,
        Descripcion = s.Descripcion,
        Estado = s.Estado,
        FechaCreacion = s.FechaCreacion,
        Paquetes = s.Paquetes.Select(PaqueteResponse.Desde).ToList(),
        Fotos = s.Fotos.Select(f => f.Url).ToList(),
    };
}

// Renglón de un listado: datos básicos sin paquetes ni fotos.
public class SalonResumenResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Zona { get; set; } = string.Empty;
    public int Capacidad { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int TotalPaquetes { get; set; }
}
