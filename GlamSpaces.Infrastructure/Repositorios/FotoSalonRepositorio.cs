namespace GlamSpaces.Infrastructure.Repositorios;

// HU-07 (fotos): registro de fotos con sp_FotoSalon_Agregar y sp_FotoSalon_Eliminar
// (Database/12_FotosSalonHU07.sql).
public class FotoSalonRepositorio : IFotoSalonRepositorio
{
    private readonly IDbConnection _db;

    public FotoSalonRepositorio(IDbConnection db)
    {
        _db = db;
    }

    public async Task<RespuestaApi<FotoSalonResponse>> Agregar(int salonId, int adminId, string url)
    {
        var p = StoredProcedure.Parametros(new { SalonId = salonId, AdminId = adminId, Url = url });
        var foto = await _db.QueryFirstOrDefaultAsync<FotoSalon>(
            "sp_FotoSalon_Agregar", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(foto, FotoSalonResponse.Desde);
    }

    public Task<RespuestaApi<FotoSalonResponse>> Validar(EliminarFotoRequest request) => Eliminar(request, soloValidar: true);

    public Task<RespuestaApi<FotoSalonResponse>> Eliminar(EliminarFotoRequest request) => Eliminar(request, soloValidar: false);

    private async Task<RespuestaApi<FotoSalonResponse>> Eliminar(EliminarFotoRequest request, bool soloValidar)
    {
        var p = StoredProcedure.Parametros(new { request.SalonId, request.FotoId, request.AdminId, SoloValidar = soloValidar });
        var foto = await _db.QueryFirstOrDefaultAsync<FotoSalon>(
            "sp_FotoSalon_Eliminar", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(foto, FotoSalonResponse.Desde);
    }
}
