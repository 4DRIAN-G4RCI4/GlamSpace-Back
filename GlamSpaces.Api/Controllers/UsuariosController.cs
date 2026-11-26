namespace GlamSpaces.Api.Controllers;

// Todos los endpoints son POST: los datos viajan en el body y no quedan en la URL ni en los logs.
[Route("api/usuarios")]
public class UsuariosController : ApiControllerBase
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly CuentaCorreoServicio _cuenta;

    public UsuariosController(IUsuarioRepositorio usuarios, CuentaCorreoServicio cuenta)
    {
        _usuarios = usuarios;
        _cuenta = cuenta;
    }

    // POST /api/usuarios/registro
    // Cubre HU-02 (registro de cliente) y HU-03 (registro de administrador de salón).
    // Sprint 10: la cuenta nace con correoVerificado = false y se le manda el código por correo.
    [HttpPost("registro")]
    public async Task<ActionResult<RespuestaApi<UsuarioResponse>>> Registrar([FromBody] RegistroRequest request)
        => Responder(await _cuenta.Registrar(request));

    // POST /api/usuarios/login
    // Cubre HU-04 (inicio de sesión). Sprint 10: si el correo no está verificado responde 1007 (403)
    // y le reenvía el código.
    [HttpPost("login")]
    public async Task<ActionResult<RespuestaApi<UsuarioResponse>>> Login([FromBody] LoginRequest request)
        => Responder(await _cuenta.Login(request));

    // POST /api/usuarios/obtener
    // Consulta un usuario por id (sin la contraseña).
    [HttpPost("obtener")]
    public async Task<ActionResult<RespuestaApi<UsuarioResponse>>> Obtener([FromBody] IdRequest request)
        => Responder(await _usuarios.Obtener(request.Id));

    // POST /api/usuarios/convertirAAdministrador  { usuarioId, contrasena }
    // Convierte una cuenta de cliente en administrador (mismo correo, contraseña e historial).
    // Pide la contraseña porque todavía no hay JWT. 1005 (404) no existe · 1003 (401) contraseña
    // incorrecta · 1006 (409) ya es administrador. Regresa el usuario con el mismo formato del login.
    [HttpPost("convertirAAdministrador")]
    public async Task<ActionResult<RespuestaApi<UsuarioResponse>>> ConvertirAAdministrador([FromBody] ConvertirAdministradorRequest request)
        => Responder(await _usuarios.ConvertirAAdministrador(request));

    // ---------- Sprint 10 · verificación del correo y recuperación de la cuenta ----------

    // POST /api/usuarios/verificarCorreo { correo, codigo } → el usuario (ya verificado: entra).
    // 1008 código incorrecto · 1009 vencido · 1010 (429) demasiados intentos.
    [HttpPost("verificarCorreo")]
    public async Task<ActionResult<RespuestaApi<UsuarioResponse>>> VerificarCorreo([FromBody] VerificarCorreoRequest request)
        => Responder(await _cuenta.VerificarCorreo(request));

    // POST /api/usuarios/reenviarCodigo { correo, proposito: "verificacion" | "recuperacion" }
    // 1011 (429) si pidió otro hace menos de 60 s. No revela si el correo tiene cuenta.
    [HttpPost("reenviarCodigo")]
    public async Task<ActionResult<RespuestaApi<object?>>> ReenviarCodigo([FromBody] ReenviarCodigoRequest request)
        => Responder(await _cuenta.ReenviarCodigo(request));

    // POST /api/usuarios/recuperar { correo } → siempre el mismo mensaje, exista o no la cuenta.
    [HttpPost("recuperar")]
    public async Task<ActionResult<RespuestaApi<object?>>> Recuperar([FromBody] RecuperarCuentaRequest request)
        => Responder(await _cuenta.Recuperar(request));

    // POST /api/usuarios/restablecer { correo, codigo, nuevaContrasena }
    [HttpPost("restablecer")]
    public async Task<ActionResult<RespuestaApi<object?>>> Restablecer([FromBody] RestablecerContrasenaRequest request)
        => Responder(await _cuenta.Restablecer(request));
}
