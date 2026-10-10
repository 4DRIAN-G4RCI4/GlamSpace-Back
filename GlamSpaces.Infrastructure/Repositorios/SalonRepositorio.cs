namespace GlamSpaces.Infrastructure.Repositorios;

public class SalonRepositorio : ISalonRepositorio
{
    private readonly IDbConnection _db;

    public SalonRepositorio(IDbConnection db)
    {
        _db = db;
    }

    public async Task<RespuestaApi<SalonResponse>> Crear(SalonRequest request)
    {
        var p = StoredProcedure.Parametros(new
        {
            request.AdminId,
            request.Nombre,
            request.Zona,
            request.Capacidad,
            request.Descripcion,
        });
        var salon = await _db.QueryFirstOrDefaultAsync<Salon>(
            "sp_Salon_Crear", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(salon, SalonResponse.Desde);
    }

    public async Task<RespuestaApi<SalonResponse>> Obtener(int id)
    {
        var p = StoredProcedure.Parametros(new { Id = id });
        Salon? salon;

        // El SP regresa 3 result sets en este orden: salón, paquetes, fotos.
        using (var resultados = await _db.QueryMultipleAsync(
            "sp_Salon_Obtener", p, commandType: CommandType.StoredProcedure))
        {
            salon = await resultados.ReadFirstOrDefaultAsync<Salon>();
            var paquetes = (await resultados.ReadAsync<Paquete>()).ToList();
            var fotos = (await resultados.ReadAsync<FotoSalon>()).ToList();
            if (salon != null)
            {
                salon.Paquetes = paquetes;
                salon.Fotos = fotos;
            }
        }

        return p.Respuesta(salon, SalonResponse.Desde);
    }

    public async Task<RespuestaApi<SalonResponse>> Actualizar(ActualizarSalonRequest request)
    {
        var p = StoredProcedure.Parametros(new
        {
            request.Id,
            request.AdminId,
            request.Nombre,
            request.Zona,
            request.Capacidad,
            request.Descripcion,
            request.Estado,
        });
        await _db.ExecuteAsync("sp_Salon_Actualizar", p, commandType: CommandType.StoredProcedure);

        if (p.Codigo() != CodigosError.Exito)
            return RespuestaApi<SalonResponse>.Error(p.Codigo(), p.Mensaje());

        // Regresa el detalle completo (con paquetes) igual que obtener.
        var detalle = await Obtener(request.Id);
        detalle.Mensaje = p.Mensaje();
        return detalle;
    }

    public async Task<RespuestaPaginada<SalonResumenResponse>> Listar(ListarSalonesRequest request)
    {
        var p = StoredProcedure.Parametros(new { request.AdminId, request.Pagina, request.TamanoPagina });
        return await _db.Paginado<SalonResumenResponse>("sp_Salon_Listar", p);
    }

    public async Task<RespuestaPaginada<SalonBusquedaResponse>> Buscar(BuscarSalonesRequest request)
    {
        var p = StoredProcedure.Parametros(new
        {
            request.Zona,
            request.CapacidadMinima,
            request.PrecioMaximo,
            request.Pagina,
            request.TamanoPagina,
        });
        return await _db.Paginado<SalonBusquedaResponse>("sp_Salon_Buscar", p);
    }
}
