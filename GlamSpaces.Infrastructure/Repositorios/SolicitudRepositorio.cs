namespace GlamSpaces.Infrastructure.Repositorios;

// HU-15. Todas las reglas (cliente, salón, paquete, capacidad, fecha disponible y duplicados)
// viven en los stored procedures de la migración M007 (Database/08_SolicitudesHU15.sql).
public class SolicitudRepositorio : ISolicitudRepositorio
{
    private readonly IDbConnection _db;

    public SolicitudRepositorio(IDbConnection db)
    {
        _db = db;
    }

    public async Task<RespuestaApi<SolicitudResponse>> Crear(CrearSolicitudRequest request)
    {
        var p = StoredProcedure.Parametros(new
        {
            request.ClienteId,
            request.SalonId,
            request.PaqueteId,
            FechaEvento = request.FechaEvento.Date,
            request.TipoEvento,
            request.NumeroInvitados,
            request.TelefonoContacto,
            request.Comentarios,
        });
        var solicitud = await _db.QueryFirstOrDefaultAsync<SolicitudResponse>(
            "sp_Solicitud_Crear", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(solicitud, s => s);
    }

    public async Task<RespuestaApi<SolicitudResponse>> Obtener(ObtenerSolicitudRequest request)
    {
        var p = StoredProcedure.Parametros(new { request.Id, request.ClienteId });
        var solicitud = await _db.QueryFirstOrDefaultAsync<SolicitudResponse>(
            "sp_Solicitud_Obtener", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(solicitud, s => s);
    }

    public async Task<RespuestaPaginada<SolicitudResponse>> Listar(ListarSolicitudesRequest request)
    {
        var p = StoredProcedure.Parametros(new { request.ClienteId, request.Pagina, request.TamanoPagina });
        return await _db.Paginado<SolicitudResponse>("sp_Solicitud_ListarPorCliente", p);
    }
}
