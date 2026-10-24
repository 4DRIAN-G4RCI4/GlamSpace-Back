var builder = WebApplication.CreateBuilder(args);

// Base de datos: Azure SQL (SQL Server en la nube).
// La cadena de conexión NO va en appsettings.json (tiene contraseña):
//  - En tu compu: dotnet user-secrets
//  - En Azure App Service: Configuración > Cadenas de conexión > GlamSpacesDb
var cadenaConexion = builder.Configuration.GetConnectionString("GlamSpacesDb");

// Una sola conexión por petición HTTP: se abre cuando un repositorio la pide y se cierra
// al terminar la petición. Todos los SP de esa petición usan la misma conexión, y el pool
// de ADO.NET reutiliza la conexión física entre peticiones.
builder.Services.AddScoped<IDbConnection>(_ =>
{
    var conexion = new SqlConnection(cadenaConexion);
    conexion.Open();
    return conexion;
});

// Repositorios (Infrastructure) detrás de sus interfaces (Domain).
builder.Services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
builder.Services.AddScoped<ISalonRepositorio, SalonRepositorio>();
builder.Services.AddScoped<IPaqueteRepositorio, PaqueteRepositorio>();

// Healthcheck para monitoreo: /health responde Healthy si la BD contesta.
builder.Services.AddHealthChecks().AddCheck("azure-sql", () =>
{
    using var conexion = new SqlConnection(cadenaConexion);
    conexion.Open();
    return HealthCheckResult.Healthy();
});

builder.Services.AddControllers().ConfigureApiBehaviorOptions(opciones =>
{
    // JSON mal formado o con tipos incorrectos también responde con el formato estándar.
    opciones.InvalidModelStateResponseFactory = contexto =>
    {
        var campos = contexto.ModelState.Where(c => c.Value!.Errors.Count > 0).Select(c => c.Key);
        return new BadRequestObjectResult(RespuestaApi<object>.Error(
            CodigosError.DatosInvalidos, $"El JSON enviado no es válido: {string.Join(", ", campos)}."));
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: solo los orígenes del frontend pueden llamar a la API desde un navegador.
// La lista vive en appsettings.json (Cors:OrigenesPermitidos); en Azure se puede cambiar
// sin recompilar con las variables Cors__OrigenesPermitidos__0, __1, ...
// (La app nativa no usa CORS; esto solo aplica a navegadores.)
var origenesPermitidos = builder.Configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];
builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy("Frontend", politica =>
    {
        politica.WithOrigins(origenesPermitidos).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Cualquier excepción no controlada (ej. la BD no responde) regresa el formato estándar
// con código 5000 en vez de una página de error. El detalle queda en el log.
app.UseExceptionHandler(errores => errores.Run(async contexto =>
{
    contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await contexto.Response.WriteAsJsonAsync(RespuestaApi<object>.Error(
        CodigosError.ErrorInterno, "Ocurrió un error inesperado. Intenta de nuevo más tarde."));
}));

// Swagger disponible en todos los entornos (también en Azure)
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Versión asíncrona: no bloquea el hilo principal mientras la API está en ejecución.
await app.RunAsync();

// Hace pública la clase Program generada por las top-level statements para que
// GlamSpaces.Tests pueda levantar la API en memoria con WebApplicationFactory<Program>.
public partial class Program;
