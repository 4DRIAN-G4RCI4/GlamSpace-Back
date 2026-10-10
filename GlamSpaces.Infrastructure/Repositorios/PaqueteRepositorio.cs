namespace GlamSpaces.Infrastructure.Repositorios;

public class PaqueteRepositorio : IPaqueteRepositorio
{
    private readonly IDbConnection _db;

    public PaqueteRepositorio(IDbConnection db)
    {
        _db = db;
    }

    public async Task<RespuestaApi<PaqueteResponse>> Crear(CrearPaqueteRequest request)
    {
        var p = StoredProcedure.Parametros(new
        {
            request.SalonId,
            request.AdminId,
            request.NombrePaquete,
            request.Descripcion,
            request.Precio,
        });
        var paquete = await _db.QueryFirstOrDefaultAsync<Paquete>(
            "sp_Paquete_Crear", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(paquete, PaqueteResponse.Desde);
    }

    public async Task<RespuestaApi<PaqueteResponse>> Actualizar(ActualizarPaqueteRequest request)
    {
        var p = StoredProcedure.Parametros(new
        {
            request.Id,
            request.AdminId,
            request.NombrePaquete,
            request.Descripcion,
            request.Precio,
        });
        var paquete = await _db.QueryFirstOrDefaultAsync<Paquete>(
            "sp_Paquete_Actualizar", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(paquete, PaqueteResponse.Desde);
    }

    public async Task<RespuestaApi<int>> Eliminar(EliminarPaqueteRequest request)
    {
        var p = StoredProcedure.Parametros(new { request.Id, request.AdminId });
        await _db.ExecuteAsync("sp_Paquete_Eliminar", p, commandType: CommandType.StoredProcedure);

        return p.Codigo() == CodigosError.Exito
            ? RespuestaApi<int>.Ok(request.Id, p.Mensaje())
            : RespuestaApi<int>.Error(p.Codigo(), p.Mensaje());
    }
}
