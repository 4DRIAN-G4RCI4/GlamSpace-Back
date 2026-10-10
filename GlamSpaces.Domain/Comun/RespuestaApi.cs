namespace GlamSpaces.Domain.Comun;

// Formato único de respuesta de toda la API.
// Codigo = 0 es éxito; cualquier otro valor viene del catálogo CodigosError.
public class RespuestaApi<T>
{
    public int Codigo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public T? Datos { get; set; }

    public bool Exito => Codigo == CodigosError.Exito;

    public static RespuestaApi<T> Ok(T datos, string mensaje) =>
        new() { Codigo = CodigosError.Exito, Mensaje = mensaje, Datos = datos };

    public static RespuestaApi<T> Error(int codigo, string mensaje) =>
        new() { Codigo = codigo, Mensaje = mensaje };
}

// Respuesta de los listados: además de los datos trae la información del paginado.
public class RespuestaPaginada<T> : RespuestaApi<List<T>>
{
    public int Pagina { get; set; }
    public int TamanoPagina { get; set; }
    public int TotalRegistros { get; set; }
    public int TotalPaginas { get; set; }
}
