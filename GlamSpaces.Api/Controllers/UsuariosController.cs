namespace GlamSpaces.Api.Controllers;

// Todos los endpoints son POST: los datos viajan en el body y no quedan en la URL ni en los logs.
[Route("api/usuarios")]
public class UsuariosController : ApiControllerBase
{
    private readonly IUsuarioRepositorio _usuarios;

    public UsuariosController(IUsuarioRepositorio usuarios)
    {
        _usuarios = usuarios;
    }

    // POST /api/usuarios/registro
    // Cubre HU-02 (registro de cliente) y HU-03 (registro de administrador de salón).
    [HttpPost("registro")]
    public async Task<ActionResult<RespuestaApi<UsuarioResponse>>> Registrar([FromBody] RegistroRequest request)
        => Responder(await _usuarios.Registrar(request));

    // POST /api/usuarios/login
    // Cubre HU-04 (inicio de sesión).
    [HttpPost("login")]
    public async Task<ActionResult<RespuestaApi<UsuarioResponse>>> Login([FromBody] LoginRequest request)
        => Responder(await _usuarios.Login(request));

    // POST /api/usuarios/obtener
    // Consulta un usuario por id (sin la contraseña).
    [HttpPost("obtener")]
    public async Task<ActionResult<RespuestaApi<UsuarioResponse>>> Obtener([FromBody] IdRequest request)
        => Responder(await _usuarios.Obtener(request.Id));
}
