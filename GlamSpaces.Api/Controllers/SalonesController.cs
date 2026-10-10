namespace GlamSpaces.Api.Controllers;

// Cubre HU-06 (CRUD de salones). Las reglas de negocio viven en los stored procedures;
// el controller solo recibe la petición y regresa la respuesta.
[Route("api/salones")]
public class SalonesController : ApiControllerBase
{
    private readonly ISalonRepositorio _salones;

    public SalonesController(ISalonRepositorio salones)
    {
        _salones = salones;
    }

    // POST /api/salones/crear
    // Crea un salón asociado al administrador. Siempre nace "no_publicado".
    [HttpPost("crear")]
    public async Task<ActionResult<RespuestaApi<SalonResponse>>> Crear([FromBody] SalonRequest request)
        => Responder(await _salones.Crear(request));

    // POST /api/salones/obtener
    // Detalle del salón con su lista completa de paquetes y fotos.
    [HttpPost("obtener")]
    public async Task<ActionResult<RespuestaApi<SalonResponse>>> Obtener([FromBody] IdRequest request)
        => Responder(await _salones.Obtener(request.Id));

    // POST /api/salones/actualizar
    // Actualiza el salón. Solo el dueño. Para publicarlo necesita al menos un paquete.
    [HttpPost("actualizar")]
    public async Task<ActionResult<RespuestaApi<SalonResponse>>> Actualizar([FromBody] ActualizarSalonRequest request)
        => Responder(await _salones.Actualizar(request));

    // POST /api/salones/listar
    // Listado paginado (de un administrador o de todos si AdminId es null).
    [HttpPost("listar")]
    public async Task<ActionResult<RespuestaPaginada<SalonResumenResponse>>> Listar([FromBody] ListarSalonesRequest request)
        => Responder(await _salones.Listar(request));

    // POST /api/salones/buscar
    // Cubre HU-09: búsqueda pública de salones publicados con filtros opcionales
    // (zona, capacidad mínima, precio máximo). Sin resultados regresa lista vacía con éxito.
    [HttpPost("buscar")]
    public async Task<ActionResult<RespuestaPaginada<SalonBusquedaResponse>>> Buscar([FromBody] BuscarSalonesRequest request)
        => Responder(await _salones.Buscar(request));
}
