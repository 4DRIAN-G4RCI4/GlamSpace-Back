namespace GlamSpaces.Api.Controllers;

// Cubre HU-06 (CRUD de salones) y HU-07 (fotos): subida real de imágenes. Las reglas de negocio
// viven en los stored procedures; el controller solo recibe la petición y regresa la respuesta.
[Route("api/salones")]
public class SalonesController : ApiControllerBase
{
    // Deja llegar archivos de hasta 10 MB para responder 2007 ("pesa más de 5 MB") con el formato
    // estándar; más grandes que esto, ASP.NET corta la petición antes.
    internal const long LimitePeticionFoto = 10 * 1024 * 1024;

    private readonly ISalonRepositorio _salones;
    private readonly FotosSalonServicio _fotos;
    private readonly ILogger<SalonesController> _logger;

    public SalonesController(ISalonRepositorio salones, FotosSalonServicio fotos, ILogger<SalonesController> logger)
    {
        _salones = salones;
        _fotos = fotos;
        _logger = logger;
    }

    // POST /api/salones/crear
    // Crea un salón asociado al administrador. Siempre nace "no_publicado".
    [HttpPost("crear")]
    public async Task<ActionResult<RespuestaApi<SalonResponse>>> Crear([FromBody] SalonRequest request)
        => Responder(await _salones.Crear(request));

    // POST /api/salones/obtener
    // Detalle del salón con su lista completa de paquetes y fotos.
    // También cubre HU-12 (detalle).
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

    // POST /api/salones/disponibilidad
    // Cubre HU-12: fechas ocupadas del salón en un mes (solo reservaciones confirmadas).
    // Un mes sin fechas ocupadas regresa lista vacía con éxito.
    [HttpPost("disponibilidad")]
    public async Task<ActionResult<RespuestaApi<DisponibilidadResponse>>> Disponibilidad([FromBody] DisponibilidadRequest request)
        => Responder(await _salones.Disponibilidad(request));

    // POST /api/salones/subirFoto  (multipart/form-data: salonId, adminId, archivo)
    // HU-07 (fotos): JPG, PNG o WEBP (por su contenido real), máximo 5 MB y 8 fotos por salón.
    // Regresa { fotoId, url }.
    [HttpPost("subirFoto")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimitePeticionFoto)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimitePeticionFoto)]
    public async Task<ActionResult<RespuestaApi<FotoSalonResponse>>> SubirFoto([FromForm] SubirFotoRequest request)
    {
        await using var contenido = request.Archivo?.OpenReadStream();
        return Responder(await _fotos.Subir(request.SalonId, request.AdminId, contenido, request.Archivo?.Length ?? 0));
    }

    // POST /api/salones/eliminarFoto  { salonId, fotoId, adminId }
    // HU-07 (fotos): borra la foto (registro y archivo). Solo el dueño del salón.
    [HttpPost("eliminarFoto")]
    public async Task<ActionResult<RespuestaApi<FotoSalonResponse>>> EliminarFoto([FromBody] EliminarFotoRequest request)
        => Responder(await _fotos.Eliminar(request));

    // POST /api/salones/eliminar  { id, adminId }
    // Mis salones: elimina el salón con sus fotos, paquetes, favoritas, solicitudes rechazadas y
    // reservaciones no confirmadas. Solo el dueño; 2010 si tiene solicitudes pendientes o confirmadas.
    // Después del COMMIT borra la carpeta de fotos como mejor esfuerzo (si falla, solo se registra).
    [HttpPost("eliminar")]
    public async Task<ActionResult<RespuestaApi<int>>> Eliminar([FromBody] EliminarSalonRequest request)
    {
        var respuesta = await _salones.Eliminar(request);
        if (respuesta.Exito && !await _fotos.EliminarFotosDeSalon(request.Id))
        {
            _logger.LogWarning("Salón {SalonId} eliminado, pero no se pudo borrar su carpeta de fotos del almacenamiento.", request.Id);
        }
        return Responder(respuesta);
    }
}

// Campos del multipart/form-data de subirFoto.
public class SubirFotoRequest
{
    public int SalonId { get; set; }
    public int AdminId { get; set; } // TODO: tomarlo del JWT cuando exista
    public IFormFile? Archivo { get; set; }
}
