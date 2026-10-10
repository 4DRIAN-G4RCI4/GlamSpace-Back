namespace GlamSpaces.Infrastructure.Datos;

// Helpers para llamar stored procedures con Dapper.
// Todos los SP reciben @Codigo INT OUTPUT y @Mensaje NVARCHAR(200) OUTPUT
// (ver Database/02_StoredProcedures.sql).
internal static class StoredProcedure
{
    public static DynamicParameters Parametros(object entrada)
    {
        var parametros = new DynamicParameters(entrada);
        parametros.Add("Codigo", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parametros.Add("Mensaje", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);
        return parametros;
    }

    // Los OUTPUT solo se pueden leer después de consumir los result sets del SP.
    public static int Codigo(this DynamicParameters p) => p.Get<int>("Codigo");
    public static string Mensaje(this DynamicParameters p) => p.Get<string>("Mensaje");

    // Ejecuta un SP paginado: primer result set = datos del paginado (se mapean directo
    // a la respuesta), segundo = los registros de la página.
    public static async Task<RespuestaPaginada<T>> Paginado<T>(
        this IDbConnection db, string storedProcedure, DynamicParameters p)
    {
        RespuestaPaginada<T> respuesta;

        using (var resultados = await db.QueryMultipleAsync(
            storedProcedure, p, commandType: CommandType.StoredProcedure))
        {
            respuesta = await resultados.ReadFirstAsync<RespuestaPaginada<T>>();
            respuesta.Datos = (await resultados.ReadAsync<T>()).ToList();
        }

        respuesta.Codigo = p.Codigo();
        respuesta.Mensaje = p.Mensaje();
        return respuesta;
    }

    // Arma la RespuestaApi según el @Codigo que regresó el SP.
    public static RespuestaApi<TDto> Respuesta<TEntidad, TDto>(
        this DynamicParameters p, TEntidad? entidad, Func<TEntidad, TDto> mapear) where TEntidad : class
        => p.Codigo() == CodigosError.Exito
            ? RespuestaApi<TDto>.Ok(mapear(entidad!), p.Mensaje())
            : RespuestaApi<TDto>.Error(p.Codigo(), p.Mensaje());
}
