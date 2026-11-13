namespace GlamSpaces.Infrastructure.Repositorios;

// HU-15 y HU-18. Todas las reglas viven en los stored procedures:
//  - crear, obtener y listar del cliente: M007 (Database/08_SolicitudesHU15.sql);
//  - aceptar y rechazar: sp_Solicitud_CambiarEstado de la M008 (HU-17, bloqueo de fecha);
//  - listado del salón para su administrador: M009 (Database/11_SolicitudesAdminHU18.sql).
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

    public async Task<RespuestaPaginada<SolicitudListadoResponse>> Listar(ListarSolicitudesRequest request)
    {
        var p = StoredProcedure.Parametros(new { request.ClienteId, request.Pagina, request.TamanoPagina });
        return await _db.Paginado<SolicitudListadoResponse>("sp_Solicitud_ListarPorCliente", p);
    }

    public Task<RespuestaApi<SolicitudAtendidaResponse>> Aceptar(AtenderSolicitudRequest request)
        => CambiarEstado(request, "confirmada");

    public Task<RespuestaApi<SolicitudAtendidaResponse>> Rechazar(AtenderSolicitudRequest request)
        => CambiarEstado(request, "rechazada");

    public async Task<RespuestaPaginada<SolicitudAdminResponse>> ListarPorSalon(ListarSolicitudesSalonRequest request)
    {
        var p = StoredProcedure.Parametros(new
        {
            request.SalonId,
            request.AdminId,
            request.Estado,
            request.Pagina,
            request.TamanoPagina,
        });
        return await _db.Paginado<SolicitudAdminResponse>("sp_Solicitud_ListarPorSalon", p);
    }

    // Aceptar = confirmar: el SP bloquea la fecha y rechaza las demás pendientes de ese salón y día.
    private async Task<RespuestaApi<SolicitudAtendidaResponse>> CambiarEstado(AtenderSolicitudRequest request, string estado)
    {
        var p = StoredProcedure.Parametros(new { request.Id, request.AdminId, Estado = estado });
        var solicitud = await _db.QueryFirstOrDefaultAsync<SolicitudAtendidaResponse>(
            "sp_Solicitud_CambiarEstado", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(solicitud, s => s);
    }
}
