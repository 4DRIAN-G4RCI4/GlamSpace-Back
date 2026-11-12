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

        // Result sets: paginado, salones de la página y (desde el script 13) las fotos de vista previa
        // de esos salones, ya limitadas a 5 por salón. Si el SP todavía es el anterior no hay tercer
        // result set y fotosPreview queda vacío, así que el orden del deploy no importa.
        RespuestaPaginada<SalonBusquedaResponse> respuesta;
        using (var resultados = await _db.QueryMultipleAsync(
            "sp_Salon_Buscar", p, commandType: CommandType.StoredProcedure))
        {
            respuesta = await resultados.ReadFirstAsync<RespuestaPaginada<SalonBusquedaResponse>>();
            respuesta.Datos = (await resultados.ReadAsync<SalonBusquedaResponse>()).ToList();

            if (!resultados.IsConsumed)
            {
                var fotos = (await resultados.ReadAsync<FotoPreviewFila>()).ToLookup(f => f.SalonId, f => f.Url);
                foreach (var salon in respuesta.Datos)
                {
                    salon.FotosPreview = fotos[salon.Id].Take(5).ToList();
                }
            }
        }

        respuesta.Codigo = p.Codigo();
        respuesta.Mensaje = p.Mensaje();
        return respuesta;
    }

    public async Task<RespuestaApi<DisponibilidadResponse>> Disponibilidad(DisponibilidadRequest request)
    {
        var p = StoredProcedure.Parametros(new { request.SalonId, request.Anio, request.Mes });
        var fechas = await _db.QueryAsync<DateTime>(
            "sp_Salon_Disponibilidad", p, commandType: CommandType.StoredProcedure);

        if (p.Codigo() != CodigosError.Exito)
            return RespuestaApi<DisponibilidadResponse>.Error(p.Codigo(), p.Mensaje());

        var disponibilidad = new DisponibilidadResponse
        {
            SalonId = request.SalonId,
            Anio = request.Anio,
            Mes = request.Mes,
            FechasOcupadas = fechas.Select(f => f.ToString("yyyy-MM-dd")).ToList(),
        };
        return RespuestaApi<DisponibilidadResponse>.Ok(disponibilidad, p.Mensaje());
    }
}

// Renglón del tercer result set de sp_Salon_Buscar: una foto de vista previa de un salón de la página.
internal sealed class FotoPreviewFila
{
    public int SalonId { get; set; }
    public string Url { get; set; } = string.Empty;
}
