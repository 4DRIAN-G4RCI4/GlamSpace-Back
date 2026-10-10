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

// CORS abierto para el frontend (Expo web corre en otro puerto).
// Para producción habría que restringir esto a los dominios reales.
builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy("FrontendDev", politica =>
    {
        politica.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
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

app.UseCors("FrontendDev");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
