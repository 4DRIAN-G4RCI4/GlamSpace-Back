namespace GlamSpaces.Infrastructure.Repositorios;

public class UsuarioRepositorio : IUsuarioRepositorio
{
    private const int LongitudMinPassword = 8;

    private readonly IDbConnection _db;

    public UsuarioRepositorio(IDbConnection db)
    {
        _db = db;
    }

    public async Task<RespuestaApi<UsuarioResponse>> Registrar(RegistroRequest request)
    {
        // La contraseña se valida y se hashea aquí: el SP nunca la recibe en texto plano.
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < LongitudMinPassword)
            return RespuestaApi<UsuarioResponse>.Error(CodigosError.DatosInvalidos,
                $"La contraseña debe tener al menos {LongitudMinPassword} caracteres.");

        var p = StoredProcedure.Parametros(new
        {
            request.NombreCompleto,
            request.Correo,
            PasswordHash = PasswordHasher.Hashear(request.Password),
            request.TipoCuenta,
            request.NombreSalon,
        });
        var usuario = await _db.QueryFirstOrDefaultAsync<Usuario>(
            "sp_Usuario_Registrar", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(usuario, UsuarioResponse.Desde);
    }

    public async Task<RespuestaApi<UsuarioResponse>> Login(LoginRequest request)
    {
        var p = StoredProcedure.Parametros(new { request.Correo });
        var usuario = await _db.QueryFirstOrDefaultAsync<Usuario>(
            "sp_Usuario_ObtenerPorCorreo", p, commandType: CommandType.StoredProcedure);

        // Mensaje genérico a propósito: no revela si falló el correo o la contraseña.
        if (usuario == null || !PasswordHasher.Verificar(request.Password ?? "", usuario.PasswordHash))
            return RespuestaApi<UsuarioResponse>.Error(CodigosError.CredencialesIncorrectas,
                "Correo o contraseña incorrectos.");

        return RespuestaApi<UsuarioResponse>.Ok(UsuarioResponse.Desde(usuario), "Inicio de sesión correcto.");
    }

    public async Task<RespuestaApi<UsuarioResponse>> Obtener(int id)
    {
        var p = StoredProcedure.Parametros(new { Id = id });
        var usuario = await _db.QueryFirstOrDefaultAsync<Usuario>(
            "sp_Usuario_Obtener", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(usuario, UsuarioResponse.Desde);
    }
}
