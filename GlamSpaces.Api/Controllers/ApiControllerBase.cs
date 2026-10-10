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
        CodigosError.NoEsDueno => StatusCodes.Status403Forbidden,
        CodigosError.UsuarioNoEncontrado or CodigosError.SalonNoEncontrado or CodigosError.PaqueteNoEncontrado
            => StatusCodes.Status404NotFound,
        CodigosError.CorreoDuplicado => StatusCodes.Status409Conflict,
        CodigosError.ErrorInterno => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest,
    }, respuesta);
}
