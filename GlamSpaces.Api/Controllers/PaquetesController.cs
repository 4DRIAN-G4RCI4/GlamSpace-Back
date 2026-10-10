namespace GlamSpaces.Api.Controllers;

// Cubre HU-06: agregar, editar y borrar paquetes de un salón. Solo el dueño del salón.
[Route("api/paquetes")]
public class PaquetesController : ApiControllerBase
{
    private readonly IPaqueteRepositorio _paquetes;

    public PaquetesController(IPaqueteRepositorio paquetes)
    {
        _paquetes = paquetes;
    }

    // POST /api/paquetes/crear
    // Agrega un paquete al salón indicado en SalonId.
    [HttpPost("crear")]
    public async Task<ActionResult<RespuestaApi<PaqueteResponse>>> Crear([FromBody] CrearPaqueteRequest request)
        => Responder(await _paquetes.Crear(request));

    // POST /api/paquetes/actualizar
    [HttpPost("actualizar")]
    public async Task<ActionResult<RespuestaApi<PaqueteResponse>>> Actualizar([FromBody] ActualizarPaqueteRequest request)
        => Responder(await _paquetes.Actualizar(request));

    // POST /api/paquetes/eliminar
    // No deja un salón publicado sin paquetes.
    [HttpPost("eliminar")]
    public async Task<ActionResult<RespuestaApi<int>>> Eliminar([FromBody] EliminarPaqueteRequest request)
        => Responder(await _paquetes.Eliminar(request));
}
