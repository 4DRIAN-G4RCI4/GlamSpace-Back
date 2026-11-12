namespace GlamSpaces.Api.Controllers;

// Base de todos los controllers. Traduce RespuestaApi.Codigo al status HTTP
// correspondiente, así el frontend puede revisar cualquiera de los dos.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult Responder<T>(RespuestaApi<T> respuesta) => StatusCode(respuesta.Codigo switch
    {
        CodigosError.Exito => StatusCodes.Status200OK,
        CodigosError.CredencialesIncorrectas => StatusCodes.Status401Unauthorized,
        CodigosError.NoEsDueno or CodigosError.SolicitudAjena => StatusCodes.Status403Forbidden,
        CodigosError.UsuarioNoEncontrado or CodigosError.SalonNoEncontrado or CodigosError.PaqueteNoEncontrado
            or CodigosError.SolicitudNoEncontrada or CodigosError.FotoNoEncontrada => StatusCodes.Status404NotFound,
        CodigosError.CorreoDuplicado or CodigosError.FechaNoDisponible or CodigosError.SolicitudDuplicada
            or CodigosError.SolicitudYaAtendida or CodigosError.LimiteDeFotos => StatusCodes.Status409Conflict,
        CodigosError.FotoMuyPesada => StatusCodes.Status413PayloadTooLarge,
        CodigosError.FormatoFotoInvalido => StatusCodes.Status415UnsupportedMediaType,
        CodigosError.ErrorInterno => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest,
    }, respuesta);
}
