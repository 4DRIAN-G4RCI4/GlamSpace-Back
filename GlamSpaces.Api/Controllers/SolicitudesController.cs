namespace GlamSpaces.Api.Controllers;

// Cubre HU-15 (el cliente envía solicitudes y consulta su estado) y HU-18 (el administrador
// del salón las lista, acepta o rechaza).
// Las reglas de negocio viven en los stored procedures; el controller solo delega.
[Route("api/solicitudes")]
public class SolicitudesController : ApiControllerBase
{
    private readonly ISolicitudRepositorio _solicitudes;

    public SolicitudesController(ISolicitudRepositorio solicitudes)
    {
        _solicitudes = solicitudes;
    }

    // POST /api/solicitudes/crear
    // Crea la solicitud en estado "pendiente". Falla si la fecha ya tiene una solicitud confirmada.
    [HttpPost("crear")]
    public async Task<ActionResult<RespuestaApi<SolicitudResponse>>> Crear([FromBody] CrearSolicitudRequest request)
        => Responder(await _solicitudes.Crear(request));

    // POST /api/solicitudes/obtener
    // Datos y estado actual de una solicitud. Solo si es del cliente que la pide.
    [HttpPost("obtener")]
    public async Task<ActionResult<RespuestaApi<SolicitudResponse>>> Obtener([FromBody] ObtenerSolicitudRequest request)
        => Responder(await _solicitudes.Obtener(request));

    // POST /api/solicitudes/listar
    // Listado paginado de las solicitudes del cliente. Sin solicitudes regresa lista vacía con éxito.
    [HttpPost("listar")]
    public async Task<ActionResult<RespuestaPaginada<SolicitudResponse>>> Listar([FromBody] ListarSolicitudesRequest request)
        => Responder(await _solicitudes.Listar(request));

    // POST /api/solicitudes/aceptar (HU-18)
    // Solo el administrador dueño del salón. Confirma la solicitud, bloquea la fecha y rechaza las
    // demás pendientes de ese salón y día (HU-17). Salón ajeno → 2002 (403); ya atendida → 4006 (409).
    [HttpPost("aceptar")]
    public async Task<ActionResult<RespuestaApi<SolicitudAtendidaResponse>>> Aceptar([FromBody] AtenderSolicitudRequest request)
        => Responder(await _solicitudes.Aceptar(request));

    // POST /api/solicitudes/rechazar (HU-18)
    // Solo el administrador dueño del salón. La fecha sigue disponible.
    [HttpPost("rechazar")]
    public async Task<ActionResult<RespuestaApi<SolicitudAtendidaResponse>>> Rechazar([FromBody] AtenderSolicitudRequest request)
        => Responder(await _solicitudes.Rechazar(request));

    // POST /api/solicitudes/listarPorSalon (HU-18)
    // Solicitudes del salón para su administrador, por fecha del evento. Por defecto solo las pendientes.
    [HttpPost("listarPorSalon")]
    public async Task<ActionResult<RespuestaPaginada<SolicitudAdminResponse>>> ListarPorSalon([FromBody] ListarSolicitudesSalonRequest request)
        => Responder(await _solicitudes.ListarPorSalon(request));
}
