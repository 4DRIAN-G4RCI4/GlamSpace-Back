namespace GlamSpaces.Domain.Dtos;

// POST /api/solicitudes/crear (HU-15). La solicitud siempre nace "pendiente".
public class CrearSolicitudRequest
{
    // TODO: tomar clienteId del JWT cuando exista la autenticación
    public int ClienteId { get; set; }
    public int SalonId { get; set; }
    public int? PaqueteId { get; set; } // opcional; si viene debe ser del mismo salón
    public DateTime FechaEvento { get; set; } // solo se usa la fecha (DATE en la BD)
    public string TipoEvento { get; set; } = string.Empty;
    public int NumeroInvitados { get; set; }
    public string TelefonoContacto { get; set; } = string.Empty;
    public string? Comentarios { get; set; }
}

// POST /api/solicitudes/obtener. Solo regresa la solicitud si es de ese cliente.
public class ObtenerSolicitudRequest
{
    public int Id { get; set; }
    // TODO: tomar clienteId del JWT cuando exista la autenticación
    public int ClienteId { get; set; }
}

// POST /api/solicitudes/listar. Las solicitudes del cliente, de la más reciente a la más antigua.
public class ListarSolicitudesRequest : PaginacionRequest
{
    // TODO: tomar clienteId del JWT cuando exista la autenticación
    public int ClienteId { get; set; }
}

// Solicitud con el nombre del salón y del paquete. La regresan los 3 endpoints.
public class SolicitudResponse
{
    public int Id { get; set; }
    public int SalonId { get; set; }
    public string NombreSalon { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public int? PaqueteId { get; set; }
    public string? NombrePaquete { get; set; } // null si no eligió paquete
    public DateTime FechaEvento { get; set; }
    public string TipoEvento { get; set; } = string.Empty;
    public int NumeroInvitados { get; set; }
    public string? TelefonoContacto { get; set; } // null en solicitudes anteriores a la HU-16
    public string? Comentarios { get; set; }
    public string Estado { get; set; } = string.Empty; // "pendiente", "confirmada" o "rechazada"
    public DateTime FechaCreacion { get; set; } // UTC
}

// POST /api/solicitudes/aceptar y /rechazar (HU-18). Solo el administrador dueño del salón.
public class AtenderSolicitudRequest
{
    public int Id { get; set; }
    // TODO: tomar adminId del JWT cuando exista la autenticación
    public int AdminId { get; set; }
}

// POST /api/solicitudes/listarPorSalon (HU-18). Ordenadas por fecha del evento (las más próximas primero).
public class ListarSolicitudesSalonRequest : PaginacionRequest
{
    public int SalonId { get; set; }
    // TODO: tomar adminId del JWT cuando exista la autenticación
    public int AdminId { get; set; }
    public string? Estado { get; set; } // "pendiente" (por defecto), "confirmada", "rechazada" o "todas"
}

// Renglón de los listados (listar y listarPorSalon): agrega la foto principal del salón.
public class SolicitudListadoResponse : SolicitudResponse
{
    public string? FotoPrincipal { get; set; } // URL de la primera foto del salón (por Id); null si no tiene
}

// Solicitud vista por el administrador del salón: agrega quién la pidió y el precio del paquete.
public class SolicitudAdminResponse : SolicitudListadoResponse
{
    public string NombreCliente { get; set; } = string.Empty;
    public decimal? PrecioPaquete { get; set; } // null si no eligió paquete
}

// Respuesta de aceptar/rechazar: la solicitud con su nuevo estado y cuántas pendientes de la
// misma fecha se rechazaron solas al aceptar (HU-17). Al rechazar siempre es 0.
public class SolicitudAtendidaResponse : SolicitudResponse
{
    public int SolicitudesRechazadas { get; set; }
}
