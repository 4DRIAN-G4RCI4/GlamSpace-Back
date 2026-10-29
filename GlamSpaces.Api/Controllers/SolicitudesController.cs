namespace GlamSpaces.Api.Controllers;

// Cubre HU-15: el cliente envía solicitudes de reservación y consulta su estado.
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
}
